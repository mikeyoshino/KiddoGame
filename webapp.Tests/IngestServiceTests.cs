using System.Net;
using System.Text;
using Kiddo.Web.Models;
using Kiddo.Web.Services;
using Microsoft.Extensions.Configuration;

namespace Kiddo.Web.Tests;

public class IngestServiceTests
{
    private static IConfiguration BuildConfig(
        string thumbnailDir, string urlPrefix,
        string openAiKey = "", string supabaseUrl = "", string serviceKey = "")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ingest:ThumbnailDir"] = thumbnailDir,
                ["Ingest:ThumbnailUrlPrefix"] = urlPrefix,
                ["Ingest:OpenAiApiKey"] = openAiKey,
                ["Supabase:Url"] = supabaseUrl,
                ["Supabase:ServiceKey"] = serviceKey,
            })
            .Build();
    }

    private static IHttpClientFactory MakeFactory(HttpMessageHandler handler) =>
        new FakeHttpClientFactory(handler);

    // ── Thumbnail download ───────────────────────────────────────────────────

    [Fact]
    public async Task DownloadThumbnailAsync_SavesFileAndReturnsLocalUrl()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var config = BuildConfig(thumbnailDir: tmpDir, urlPrefix: "/images/games");
        var factory = MakeFactory(new FakeBytesHandler([1, 2, 3]));
        var svc = new IngestService(factory, config);

        var result = await svc.DownloadThumbnailAsync("abc123",
            "https://img.gamedistribution.com/abc123-512x384.jpg");

        Assert.NotNull(result);
        Assert.StartsWith("/images/games/abc123", result);
        Assert.True(File.Exists(Path.Combine(tmpDir, "abc123.jpg")));
        Directory.Delete(tmpDir, recursive: true);
    }

    [Fact]
    public async Task DownloadThumbnailAsync_ReturnsNullWhenAllExtensionsFail()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmpDir);
        var config = BuildConfig(thumbnailDir: tmpDir, urlPrefix: "/images/games");
        var factory = MakeFactory(new StatusHandler(HttpStatusCode.NotFound));
        var svc = new IngestService(factory, config);

        var result = await svc.DownloadThumbnailAsync("abc123",
            "https://img.gamedistribution.com/abc123.jpg");

        Assert.Null(result);
        Directory.Delete(tmpDir, recursive: true);
    }

    [Fact]
    public async Task DownloadThumbnailAsync_FallsBackToAlternateExtension()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var config = BuildConfig(thumbnailDir: tmpDir, urlPrefix: "/images/games");
        var factory = MakeFactory(new SequenceHandler([
            new HttpResponseMessage(HttpStatusCode.NotFound),   // .jpg fails
            new HttpResponseMessage(HttpStatusCode.NotFound),   // .jpeg fails
            new HttpResponseMessage(HttpStatusCode.OK)          // .png succeeds
            {
                Content = new ByteArrayContent([1, 2, 3])
            },
        ]));
        var svc = new IngestService(factory, config);

        var result = await svc.DownloadThumbnailAsync("abc123",
            "https://img.gamedistribution.com/abc123.jpg");

        Assert.NotNull(result);
        Assert.Contains("abc123", result);
        Directory.Delete(tmpDir, recursive: true);
    }

    // ── OpenAI translation ───────────────────────────────────────────────────

    [Fact]
    public async Task TranslateBatchAsync_ReturnsParsedTranslations()
    {
        var openAiJson = """
        {
            "choices": [{
                "message": {
                    "content": "{\"translations\":[{\"object_id\":\"abc\",\"description_th\":\"เกมสนุก\",\"instruction_th\":\"กดปุ่ม\"}]}"
                }
            }]
        }
        """;
        var config = BuildConfig("/tmp", "/images", openAiKey: "sk-test");
        var factory = MakeFactory(new FakeHandler(openAiJson));
        var svc = new IngestService(factory, config);

        var games = new[]
        {
            new IngestGame("abc", "cool", "Cool Game", null,
                "https://img.example.com/img.jpg", "Fun game", "Press button",
                [], [], [], [], [])
        };
        var result = await svc.TranslateBatchAsync(games);

        Assert.True(result.ContainsKey("abc"));
        Assert.Equal("เกมสนุก", result["abc"].DescTh);
        Assert.Equal("กดปุ่ม", result["abc"].InstrTh);
    }

    [Fact]
    public async Task TranslateBatchAsync_ReturnsEmptyWhenNoApiKey()
    {
        var config = BuildConfig("/tmp", "/images", openAiKey: "");
        var factory = MakeFactory(new FakeHandler("{}"));
        var svc = new IngestService(factory, config);

        var result = await svc.TranslateBatchAsync([]);

        Assert.Empty(result);
    }

    [Fact]
    public async Task TranslateBatchAsync_ReturnsEmptyOnOpenAiFailure()
    {
        var config = BuildConfig("/tmp", "/images", openAiKey: "sk-test");
        var factory = MakeFactory(new StatusHandler(HttpStatusCode.InternalServerError));
        var svc = new IngestService(factory, config);

        var games = new[]
        {
            new IngestGame("abc", "cool", "Cool", null,
                "http://x.com/img.jpg", null, null, [], [], [], [], [])
        };
        var result = await svc.TranslateBatchAsync(games);

        Assert.Empty(result);
    }

    // ── Filter new ───────────────────────────────────────────────────────────

    [Fact]
    public async Task FilterNewAsync_ExcludesDoneIds()
    {
        var supabaseJson = """[{"object_id":"id1","status":"done"}]""";
        var config = BuildConfig("/tmp", "/images",
            supabaseUrl: "https://fake.supabase.co", serviceKey: "svc");
        var factory = MakeFactory(new FakeHandler(supabaseJson));
        var svc = new IngestService(factory, config);

        var result = await svc.FilterNewAsync(["id1", "id2", "id3"]);

        Assert.Equal(2, result.Length);
        Assert.Contains("id2", result);
        Assert.Contains("id3", result);
    }

    [Fact]
    public async Task FilterNewAsync_IncludesPendingIds()
    {
        var supabaseJson = """
            [{"object_id":"id1","status":"done"},{"object_id":"id2","status":"pending"}]
            """;
        var config = BuildConfig("/tmp", "/images",
            supabaseUrl: "https://fake.supabase.co", serviceKey: "svc");
        var factory = MakeFactory(new FakeHandler(supabaseJson));
        var svc = new IngestService(factory, config);

        var result = await svc.FilterNewAsync(["id1", "id2"]);

        Assert.Single(result);
        Assert.Equal("id2", result[0]);
    }

    [Fact]
    public async Task FilterNewAsync_EmptyInputReturnsEmpty()
    {
        var config = BuildConfig("/tmp", "/images",
            supabaseUrl: "https://fake.supabase.co", serviceKey: "svc");
        var factory = MakeFactory(new FakeHandler("[]"));
        var svc = new IngestService(factory, config);

        var result = await svc.FilterNewAsync([]);

        Assert.Empty(result);
    }

    // ── Supabase upsert ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertGamesAsync_SendsPostWithMergeDuplicatesHeader()
    {
        var captured = new List<HttpRequestMessage>();
        var config = BuildConfig("/tmp", "/images",
            supabaseUrl: "https://fake.supabase.co", serviceKey: "svc-key");
        var factory = MakeFactory(new CapturingHandler("[]", captured));
        var svc = new IngestService(factory, config);

        var game = new IngestGame("abc", "cool", "Cool", null,
            "http://img.example.com/img.jpg", null, null, [], [], [], [], []);

        await svc.UpsertGamesAsync(
            [game],
            new Dictionary<string, string?> { ["abc"] = "/images/games/abc.jpg" },
            []);

        Assert.Single(captured);
        Assert.Equal(HttpMethod.Post, captured[0].Method);
        Assert.Contains("games", captured[0].RequestUri!.AbsolutePath);
        Assert.Equal("resolution=merge-duplicates",
            captured[0].Headers.GetValues("Prefer").First());
    }

    [Fact]
    public async Task UpsertGamesAsync_SetsPendingWhenThumbnailFailed()
    {
        var captured = new List<HttpRequestMessage>();
        var config = BuildConfig("/tmp", "/images",
            supabaseUrl: "https://fake.supabase.co", serviceKey: "svc");
        var factory = MakeFactory(new CapturingHandler("[]", captured));
        var svc = new IngestService(factory, config);

        var game = new IngestGame("abc", "cool", "Cool", null,
            "http://img.example.com/img.jpg", null, null, [], [], [], [], []);

        await svc.UpsertGamesAsync(
            [game],
            new Dictionary<string, string?> { ["abc"] = null },
            []);

        var body = await captured[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"status\":\"pending\"", body);
        Assert.Contains("http://img.example.com/img.jpg", body);
    }

    [Fact]
    public async Task UpsertGamesAsync_SetsDoneWhenThumbnailSucceeded()
    {
        var captured = new List<HttpRequestMessage>();
        var config = BuildConfig("/tmp", "/images",
            supabaseUrl: "https://fake.supabase.co", serviceKey: "svc");
        var factory = MakeFactory(new CapturingHandler("[]", captured));
        var svc = new IngestService(factory, config);

        var game = new IngestGame("abc", "cool", "Cool", null,
            "http://img.example.com/img.jpg", null, null, [], [], [], [], []);

        await svc.UpsertGamesAsync(
            [game],
            new Dictionary<string, string?> { ["abc"] = "/images/games/abc.jpg" },
            []);

        var body = await captured[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"status\":\"done\"", body);
        Assert.Contains("/images/games/abc.jpg", body);
    }

    // ── CheckTitleDuplicatesAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CheckTitleDuplicatesAsync_ReturnsExistingTitles()
    {
        var supabaseJson = """[{"title":"Angry Birds"},{"title":"Fruit Ninja"}]""";
        var config = BuildConfig("/tmp", "/images",
            supabaseUrl: "https://fake.supabase.co", serviceKey: "svc");
        var factory = MakeFactory(new FakeHandler(supabaseJson));
        var svc = new IngestService(factory, config);

        var result = await svc.CheckTitleDuplicatesAsync(["Angry Birds", "New Game", "Fruit Ninja"]);

        Assert.Equal(2, result.Length);
        Assert.Contains("Angry Birds", result);
        Assert.Contains("Fruit Ninja", result);
    }

    [Fact]
    public async Task CheckTitleDuplicatesAsync_EmptyInputReturnsEmpty()
    {
        var config = BuildConfig("/tmp", "/images",
            supabaseUrl: "https://fake.supabase.co", serviceKey: "svc");
        var factory = MakeFactory(new AssertNotCalledHandler());
        var svc = new IngestService(factory, config);

        var result = await svc.CheckTitleDuplicatesAsync([]);

        Assert.Empty(result);
    }

    // ── UpsertGamesAsync — provider fields + pre-translation ─────────────────

    [Fact]
    public async Task UpsertGamesAsync_StoresProviderFieldsAndPreTranslation()
    {
        var captured = new List<HttpRequestMessage>();
        var config = BuildConfig("/tmp", "/images",
            supabaseUrl: "https://fake.supabase.co", serviceKey: "svc");
        var factory = MakeFactory(new CapturingHandler("[]", captured));
        var svc = new IngestService(factory, config);

        var game = new IngestGame(
            "gp_123", "cool-game", "Cool Game", null,
            "http://img.gamepix.com/img.jpg", "Fun game", null,
            ["Action"], [], [], [], [],
            Provider: "GamePix",
            ProviderGameId: "123",
            GameUrl: "https://gamepix.com/play/cool-game",
            DescriptionTh: "เกมสนุก",
            InstructionTh: null,
            TranslationStatus: "translated"
        );

        await svc.UpsertGamesAsync(
            [game],
            new Dictionary<string, string?> { ["gp_123"] = "/images/games/gp_123.jpg" },
            []);

        var body = await captured[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"provider\":\"GamePix\"", body);
        Assert.Contains("\"provider_game_id\":\"123\"", body);
        Assert.Contains("\"game_url\":\"https://gamepix.com/play/cool-game\"", body);
        Assert.Contains("\"description_th\":\"เกมสนุก\"", body);
        Assert.Contains("\"translation_status\":\"translated\"", body);
    }

    // ── TranslateBatchAsync — GamePix skip ───────────────────────────────────

    [Fact]
    public async Task TranslateBatchAsync_SkipsGamePixPreTranslatedGames()
    {
        // AssertNotCalledHandler throws if HTTP is called — proves OpenAI is skipped
        var config = BuildConfig("/tmp", "/images", openAiKey: "sk-test");
        var factory = MakeFactory(new AssertNotCalledHandler());
        var svc = new IngestService(factory, config);

        var games = new[]
        {
            new IngestGame("gp_123", "cool", "Cool", null,
                "http://x.com/img.jpg", "Fun", null, [], [], [], [], [],
                Provider: "GamePix", TranslationStatus: "translated")
        };
        var result = await svc.TranslateBatchAsync(games);

        Assert.Empty(result);
    }
}

// ── Test helpers ─────────────────────────────────────────────────────────────

internal class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler);
}

internal class FakeBytesHandler(byte[] data) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage req, CancellationToken ct)
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(data)
        });
    }
}

internal class StatusHandler(HttpStatusCode code) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage req, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(code));
}

internal class AssertNotCalledHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage req, CancellationToken ct)
        => throw new InvalidOperationException("HTTP should not have been called");
}

internal class SequenceHandler(IEnumerable<HttpResponseMessage> responses) : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _queue = new(responses);
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage req, CancellationToken ct)
        => Task.FromResult(_queue.Dequeue());
}

internal class CapturingHandler(string json, List<HttpRequestMessage> captured) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage req, CancellationToken ct)
    {
        captured.Add(req);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }
}
