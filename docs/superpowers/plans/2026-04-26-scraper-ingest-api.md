# Scraper → Webapp Ingest API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the scraper's direct Supabase + filesystem writes with two minimal API endpoints on the Blazor webapp that handle thumbnail download, Thai translation, and Supabase upsert.

**Architecture:** Scraper posts batches of 10 games to `POST /api/ingest/batch` on the VPS webapp. Before fetching details, it calls `POST /api/ingest/filter-new` per GD listing page to get only new/pending object IDs. The webapp endpoint downloads thumbnails to wwwroot, calls OpenAI GPT-4o-mini for Thai translation, then upserts to Supabase — all synchronously. Scraper only needs `WEBAPP_URL` in its env.

**Tech Stack:** C# .NET 8 Blazor Server (webapp); Python 3.12 / aiohttp (scraper); xUnit + FakeHandler pattern (C# tests); pytest + pytest-asyncio + aioresponses (Python tests); Supabase REST API; OpenAI GPT-4o-mini.

---

## File Map

**New:**
- `webapp/Models/IngestModels.cs` — request/response records for ingest endpoints
- `webapp/Services/IngestService.cs` — DownloadThumbnailAsync, TranslateBatchAsync, FilterNewAsync, UpsertGamesAsync
- `webapp.Tests/IngestServiceTests.cs` — xUnit tests + test helpers
- `scraper/webapp_client.py` — filter_new(), post_batch()
- `scraper/tests/test_webapp_client.py` — pytest tests for webapp_client
- `scraper/tests/test_main.py` — pytest tests for main._send_batches

**Modified:**
- `webapp/appsettings.json` — add Supabase:ServiceKey + Ingest section
- `webapp/appsettings.Development.json` — same
- `webapp/Program.cs` — JSON options, service registration, two new endpoints
- `scraper/config.py` — add WEBAPP_URL, make Supabase/thumbnail vars optional
- `scraper/main.py` — simplified flow using webapp_client
- `scraper/requirements.txt` — add aioresponses

---

### Task 1: Webapp config + ingest models

**Files:**
- Modify: `webapp/appsettings.json`
- Modify: `webapp/appsettings.Development.json`
- Create: `webapp/Models/IngestModels.cs`

- [ ] **Step 1: Update appsettings.json**

Replace the existing content of `webapp/appsettings.json` with:

```json
{
  "Supabase": {
    "Url": "https://yaodysjicyalxshguhjj.supabase.co",
    "Key": "your-anon-key-here",
    "ServiceKey": "your-service-role-key-here"
  },
  "Ingest": {
    "OpenAiApiKey": "",
    "ThumbnailDir": "wwwroot/images/games",
    "ThumbnailUrlPrefix": "/images/games"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

Do the same for `webapp/appsettings.Development.json` (add the same `Supabase:ServiceKey` and `Ingest` sections, keeping existing dev-specific values).

- [ ] **Step 2: Create IngestModels.cs**

```csharp
namespace Kiddo.Web.Models;

public record IngestGame(
    string ObjectId,
    string Slug,
    string Title,
    string? Company,
    string ThumbnailUrl,
    string? Description,
    string? Instruction,
    string[] Categories,
    string[] Tags,
    string[] Languages,
    string[] Gender,
    string[] AgeGroup
);

public record IngestBatchRequest(IngestGame[] Games);
public record IngestResult(string ObjectId, bool Ok, string? Error = null);
public record IngestBatchResponse(IngestResult[] Results);
```

- [ ] **Step 3: Verify it compiles**

```bash
dotnet build webapp/
```
Expected: `Build succeeded, 0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
git add webapp/appsettings.json webapp/appsettings.Development.json webapp/Models/IngestModels.cs
git commit -m "feat: add ingest config and request/response models"
```

---

### Task 2: IngestService — thumbnail download

**Files:**
- Create: `webapp/Services/IngestService.cs`
- Create: `webapp.Tests/IngestServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Create `webapp.Tests/IngestServiceTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests — confirm they fail**

```bash
dotnet test webapp.Tests/ --filter "ClassName=Kiddo.Web.Tests.IngestServiceTests"
```
Expected: FAIL — `IngestService` does not exist yet.

- [ ] **Step 3: Create IngestService.cs**

```csharp
using System.Text.Json;
using Kiddo.Web.Models;
using Microsoft.Extensions.Configuration;

namespace Kiddo.Web.Services;

public class IngestService(IHttpClientFactory httpFactory, IConfiguration config)
{
    private static readonly string[] _extensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    public async Task<string?> DownloadThumbnailAsync(string objectId, string remoteUrl)
    {
        var thumbnailDir = config["Ingest:ThumbnailDir"]!;
        var urlPrefix = config["Ingest:ThumbnailUrlPrefix"]!;

        var lastDot = remoteUrl.LastIndexOf('.');
        var baseUrl = lastDot >= 0 ? remoteUrl[..lastDot] : remoteUrl;
        var currentExt = lastDot >= 0 ? remoteUrl[lastDot..].ToLower() : "";

        var candidates = new List<string> { remoteUrl };
        foreach (var ext in _extensions)
            if (ext != currentExt) candidates.Add(baseUrl + ext);

        var client = httpFactory.CreateClient("thumbnail");
        foreach (var url in candidates)
        {
            try
            {
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) continue;

                var data = await response.Content.ReadAsByteArrayAsync();
                var ext = Path.GetExtension(new Uri(url).AbsolutePath);
                if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                var filename = $"{objectId}{ext}";

                Directory.CreateDirectory(thumbnailDir);
                await File.WriteAllBytesAsync(Path.Combine(thumbnailDir, filename), data);
                return $"{urlPrefix}/{filename}";
            }
            catch { continue; }
        }
        return null;
    }
}
```

- [ ] **Step 4: Run thumbnail tests — all should pass**

```bash
dotnet test webapp.Tests/ --filter "ClassName=Kiddo.Web.Tests.IngestServiceTests"
```
Expected: 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add webapp/Services/IngestService.cs webapp.Tests/IngestServiceTests.cs
git commit -m "feat: add IngestService with thumbnail download"
```

---

### Task 3: IngestService — OpenAI translation

**Files:**
- Modify: `webapp/Services/IngestService.cs`
- Modify: `webapp.Tests/IngestServiceTests.cs`

- [ ] **Step 1: Add failing tests — append inside the `IngestServiceTests` class**

```csharp
// ── OpenAI translation ───────────────────────────────────────────────────────

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
```

- [ ] **Step 2: Run — confirm 3 new tests fail**

```bash
dotnet test webapp.Tests/ --filter "ClassName=Kiddo.Web.Tests.IngestServiceTests"
```
Expected: 3 pass (thumbnail), 3 fail (TranslateBatchAsync not found).

- [ ] **Step 3: Add TranslateBatchAsync to IngestService.cs**

Add after `DownloadThumbnailAsync`:

```csharp
private const string _systemPrompt = """
    You are a professional Thai translator for a kids' game website.
    Rules you MUST follow:
    1. Translate "description" and "instruction" from English into Thai.
    2. Game titles (proper nouns) MUST remain in English exactly as-is.
    3. Keep translations natural, friendly, suitable for children aged 5-12.
    4. Return ONLY a valid JSON object with key 'translations' containing an array.
    5. Each item must have: "object_id" (copy unchanged), "description_th" (Thai or null), "instruction_th" (Thai or null).
    6. Do NOT include any explanation, markdown, or extra text.
    """;

public async Task<Dictionary<string, (string? DescTh, string? InstrTh)>> TranslateBatchAsync(
    IngestGame[] games)
{
    var openAiKey = config["Ingest:OpenAiApiKey"];
    if (string.IsNullOrEmpty(openAiKey)) return [];

    var items = games.Select(g => new
    {
        object_id = g.ObjectId,
        title = g.Title,
        description = g.Description ?? "",
        instruction = g.Instruction ?? "",
    });
    var userMsg = $"Return translations in a JSON object with key 'translations' containing the array:\n{JsonSerializer.Serialize(items)}";
    var payload = new
    {
        model = "gpt-4o-mini",
        messages = new[]
        {
            new { role = "system", content = _systemPrompt },
            new { role = "user", content = userMsg },
        },
        response_format = new { type = "json_object" },
        temperature = 0.2,
    };

    try
    {
        var client = httpFactory.CreateClient("openai");
        var response = await client.PostAsJsonAsync(
            "https://api.openai.com/v1/chat/completions", payload);
        if (!response.IsSuccessStatusCode) return [];
        return ParseTranslations(await response.Content.ReadAsStringAsync());
    }
    catch { return []; }
}

private static Dictionary<string, (string? DescTh, string? InstrTh)> ParseTranslations(
    string rawResponse)
{
    try
    {
        using var doc = JsonDocument.Parse(rawResponse);
        var messageContent = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()!;

        using var inner = JsonDocument.Parse(messageContent);
        var result = new Dictionary<string, (string?, string?)>();
        foreach (var item in inner.RootElement.GetProperty("translations").EnumerateArray())
        {
            var id = item.GetProperty("object_id").GetString()!;
            var descTh = item.TryGetProperty("description_th", out var d)
                && d.ValueKind != JsonValueKind.Null ? d.GetString() : null;
            var instrTh = item.TryGetProperty("instruction_th", out var i)
                && i.ValueKind != JsonValueKind.Null ? i.GetString() : null;
            result[id] = (descTh, instrTh);
        }
        return result;
    }
    catch { return []; }
}
```

- [ ] **Step 4: Run all IngestService tests — all 6 should pass**

```bash
dotnet test webapp.Tests/ --filter "ClassName=Kiddo.Web.Tests.IngestServiceTests"
```
Expected: 6 tests pass.

- [ ] **Step 5: Commit**

```bash
git add webapp/Services/IngestService.cs webapp.Tests/IngestServiceTests.cs
git commit -m "feat: add OpenAI Thai translation to IngestService"
```

---

### Task 4: IngestService — FilterNewAsync + UpsertGamesAsync

**Files:**
- Modify: `webapp/Services/IngestService.cs`
- Modify: `webapp.Tests/IngestServiceTests.cs`

- [ ] **Step 1: Add failing tests — append inside the `IngestServiceTests` class**

```csharp
// ── Filter new ───────────────────────────────────────────────────────────────

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

// ── Supabase upsert ──────────────────────────────────────────────────────────

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
```

- [ ] **Step 2: Run — confirm 6 new tests fail**

```bash
dotnet test webapp.Tests/ --filter "ClassName=Kiddo.Web.Tests.IngestServiceTests"
```
Expected: 6 pass, 6 fail.

- [ ] **Step 3: Add FilterNewAsync to IngestService.cs**

```csharp
public async Task<string[]> FilterNewAsync(string[] ids)
{
    if (ids.Length == 0) return [];

    var supabaseUrl = config["Supabase:Url"]!;
    var serviceKey = config["Supabase:ServiceKey"]!;
    var inList = string.Join(",", ids);

    var client = httpFactory.CreateClient("supabase-ingest");
    var request = new HttpRequestMessage(HttpMethod.Get,
        $"{supabaseUrl}/rest/v1/games?select=object_id,status&object_id=in.({inList})");
    request.Headers.Add("apikey", serviceKey);
    request.Headers.Add("Authorization", $"Bearer {serviceKey}");

    var response = await client.SendAsync(request);
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    using var doc = JsonDocument.Parse(content);

    var doneIds = doc.RootElement.EnumerateArray()
        .Where(r => r.GetProperty("status").GetString() == "done")
        .Select(r => r.GetProperty("object_id").GetString()!)
        .ToHashSet();

    return ids.Where(id => !doneIds.Contains(id)).ToArray();
}
```

- [ ] **Step 4: Add UpsertGamesAsync to IngestService.cs**

```csharp
public async Task UpsertGamesAsync(
    IngestGame[] games,
    Dictionary<string, string?> thumbnails,
    Dictionary<string, (string? DescTh, string? InstrTh)> translations)
{
    var supabaseUrl = config["Supabase:Url"]!;
    var serviceKey = config["Supabase:ServiceKey"]!;

    var rows = games.Select(g =>
    {
        var thumbOk = thumbnails.TryGetValue(g.ObjectId, out var localUrl) && localUrl != null;
        var hasTrans = translations.TryGetValue(g.ObjectId, out var tr);
        return new
        {
            object_id = g.ObjectId,
            slug = g.Slug,
            title = g.Title,
            company = g.Company,
            thumbnail_url = thumbOk ? localUrl : g.ThumbnailUrl,
            description = g.Description,
            instruction = g.Instruction,
            description_th = hasTrans ? tr.DescTh : (string?)null,
            instruction_th = hasTrans ? tr.InstrTh : (string?)null,
            translation_status = hasTrans ? "translated" : (string?)null,
            categories = g.Categories,
            tags = g.Tags,
            languages = g.Languages,
            gender = g.Gender,
            age_group = g.AgeGroup,
            status = thumbOk ? "done" : "pending",
        };
    }).ToArray();

    var client = httpFactory.CreateClient("supabase-ingest");
    var request = new HttpRequestMessage(HttpMethod.Post, $"{supabaseUrl}/rest/v1/games")
    {
        Content = JsonContent.Create(rows),
    };
    request.Headers.Add("apikey", serviceKey);
    request.Headers.Add("Authorization", $"Bearer {serviceKey}");
    request.Headers.Add("Prefer", "resolution=merge-duplicates");

    var response = await client.SendAsync(request);
    response.EnsureSuccessStatusCode();
}
```

- [ ] **Step 5: Run all IngestService tests — all 12 should pass**

```bash
dotnet test webapp.Tests/ --filter "ClassName=Kiddo.Web.Tests.IngestServiceTests"
```
Expected: 12 tests pass.

- [ ] **Step 6: Commit**

```bash
git add webapp/Services/IngestService.cs webapp.Tests/IngestServiceTests.cs
git commit -m "feat: add FilterNewAsync and UpsertGamesAsync to IngestService"
```

---

### Task 5: Register IngestService + add endpoints to Program.cs

**Files:**
- Modify: `webapp/Program.cs`

- [ ] **Step 1: Add JSON config + service/client registrations**

After `builder.Services.AddScoped<FavoritesService>();`, add:

```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddHttpClient("thumbnail", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:150.0) Gecko/20100101 Firefox/150.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient("supabase-ingest");
builder.Services.AddHttpClient("openai");
builder.Services.AddScoped<IngestService>();
```

- [ ] **Step 2: Add filter-new endpoint**

After `app.UseAntiforgery();`, before `app.MapRazorComponents`, add:

```csharp
app.MapPost("/api/ingest/filter-new", async (string[] ids, IngestService ingestSvc) =>
{
    var newIds = await ingestSvc.FilterNewAsync(ids);
    return Results.Ok(newIds);
});
```

- [ ] **Step 3: Add batch ingest endpoint**

```csharp
app.MapPost("/api/ingest/batch", async (
    Kiddo.Web.Models.IngestBatchRequest req, IngestService ingestSvc) =>
{
    var games = req.Games;

    var thumbUrls = await Task.WhenAll(
        games.Select(g => ingestSvc.DownloadThumbnailAsync(g.ObjectId, g.ThumbnailUrl)));

    var thumbnails = games
        .Select((g, i) => (g.ObjectId, Url: thumbUrls[i]))
        .ToDictionary(x => x.ObjectId, x => x.Url);

    var translations = await ingestSvc.TranslateBatchAsync(games);

    await ingestSvc.UpsertGamesAsync(games, thumbnails, translations);

    var results = games.Select(g =>
    {
        var thumbOk = thumbnails.TryGetValue(g.ObjectId, out var url) && url != null;
        return thumbOk
            ? new Kiddo.Web.Models.IngestResult(g.ObjectId, true)
            : new Kiddo.Web.Models.IngestResult(g.ObjectId, false, "thumbnail: all extensions failed");
    }).ToArray();

    return Results.Ok(new Kiddo.Web.Models.IngestBatchResponse(results));
});
```

- [ ] **Step 4: Build and run all tests**

```bash
dotnet build webapp/ && dotnet test webapp.Tests/
```
Expected: Build succeeded, all tests pass.

- [ ] **Step 5: Commit**

```bash
git add webapp/Program.cs
git commit -m "feat: register IngestService and add /api/ingest endpoints"
```

---

### Task 6: Scraper config + test dependency

**Files:**
- Modify: `scraper/requirements.txt`
- Modify: `scraper/config.py`

- [ ] **Step 1: Add aioresponses to requirements.txt**

```
aiohttp>=3.9.5
requests>=2.31.0
supabase>=2.7.0
python-dotenv>=1.0.1
pytest>=8.1.1
pytest-asyncio>=0.23.6
aioresponses>=0.7.6
```

- [ ] **Step 2: Install it in the venv**

```bash
cd scraper && pip install aioresponses
```
Expected: `Successfully installed aioresponses-...`.

- [ ] **Step 3: Replace config.py**

```python
import os
from pathlib import Path
from dotenv import load_dotenv

load_dotenv()

# Required for main.py scraping flow
WEBAPP_URL: str = os.environ["WEBAPP_URL"]
CONCURRENCY: int = int(os.getenv("CONCURRENCY", "5"))
PAGE_DELAY: float = float(os.getenv("PAGE_DELAY", "1.0"))

# Used only by standalone utility scripts (db.py, translate.py, thumbnail_downloader.py)
SUPABASE_URL: str = os.getenv("SUPABASE_URL", "")
SUPABASE_KEY: str = os.getenv("SUPABASE_KEY", "")
OPENAI_API_KEY: str = os.getenv("OPENAI_API_KEY", "")
_DEFAULT_THUMB_DIR = Path(__file__).parent.parent / "webapp" / "wwwroot" / "images" / "games"
THUMBNAIL_OUTPUT_DIR: Path = Path(os.getenv("THUMBNAIL_OUTPUT_DIR", str(_DEFAULT_THUMB_DIR)))
THUMBNAIL_URL_PREFIX: str = os.getenv("THUMBNAIL_URL_PREFIX", "/images/games")
THUMBNAIL_CONCURRENCY: int = int(os.getenv("THUMBNAIL_CONCURRENCY", "2"))
THUMBNAIL_DELAY: float = float(os.getenv("THUMBNAIL_DELAY", "0.5"))
```

- [ ] **Step 4: Add WEBAPP_URL to scraper .env**

In `scraper/.env`, add the line:
```
WEBAPP_URL=http://localhost:5000
```
(Change to the VPS URL for production use.)

- [ ] **Step 5: Verify existing scraper tests still pass**

```bash
cd scraper && python -m pytest tests/test_gd_client.py tests/test_detail_fetcher.py -v
```
Expected: All existing tests pass.

- [ ] **Step 6: Commit**

```bash
git add scraper/requirements.txt scraper/config.py
git commit -m "feat: add WEBAPP_URL to scraper config, make Supabase vars optional"
```

---

### Task 7: webapp_client.py

**Files:**
- Create: `scraper/webapp_client.py`
- Create: `scraper/tests/test_webapp_client.py`

- [ ] **Step 1: Write failing tests**

Create `scraper/tests/test_webapp_client.py`:

```python
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import pytest
from unittest.mock import patch
from aioresponses import aioresponses
import webapp_client


@pytest.mark.asyncio
async def test_filter_new_returns_subset_from_server():
    with aioresponses() as m:
        m.post("http://testapp/api/ingest/filter-new", payload=["id2", "id3"])
        with patch.object(webapp_client, "WEBAPP_URL", "http://testapp"):
            result = await webapp_client.filter_new(["id1", "id2", "id3"])
    assert result == ["id2", "id3"]


@pytest.mark.asyncio
async def test_filter_new_returns_empty_for_empty_input():
    result = await webapp_client.filter_new([])
    assert result == []


@pytest.mark.asyncio
async def test_post_batch_returns_results():
    with aioresponses() as m:
        m.post("http://testapp/api/ingest/batch", payload={
            "results": [{"object_id": "abc", "ok": True, "error": None}]
        })
        with patch.object(webapp_client, "WEBAPP_URL", "http://testapp"):
            result = await webapp_client.post_batch([{
                "object_id": "abc", "slug": "cool", "title": "Cool",
                "company": None, "thumbnail_url": "https://img.example.com/img.jpg",
                "description": None, "instruction": None,
                "categories": [], "tags": [], "languages": [], "gender": [], "age_group": []
            }])
    assert result == [{"object_id": "abc", "ok": True, "error": None}]


@pytest.mark.asyncio
async def test_post_batch_raises_on_server_error():
    with aioresponses() as m:
        m.post("http://testapp/api/ingest/batch", status=500)
        with patch.object(webapp_client, "WEBAPP_URL", "http://testapp"):
            with pytest.raises(Exception):
                await webapp_client.post_batch([{"object_id": "abc"}])
```

- [ ] **Step 2: Run — confirm they fail**

```bash
cd scraper && python -m pytest tests/test_webapp_client.py -v
```
Expected: FAIL — `webapp_client` module not found.

- [ ] **Step 3: Create webapp_client.py**

```python
import aiohttp
from config import WEBAPP_URL

_FILTER_TIMEOUT = aiohttp.ClientTimeout(total=30)
_BATCH_TIMEOUT = aiohttp.ClientTimeout(total=120)


async def filter_new(object_ids: list[str]) -> list[str]:
    """Return object_ids that are new or pending (not yet done in the webapp DB)."""
    if not object_ids:
        return []
    async with aiohttp.ClientSession() as session:
        async with session.post(
            f"{WEBAPP_URL}/api/ingest/filter-new",
            json=object_ids,
            timeout=_FILTER_TIMEOUT,
        ) as resp:
            resp.raise_for_status()
            return await resp.json()


async def post_batch(games: list[dict]) -> list[dict]:
    """Send a batch of up to 10 games to the webapp ingest endpoint.

    Returns per-game results: [{object_id, ok, error}, ...]
    """
    async with aiohttp.ClientSession() as session:
        async with session.post(
            f"{WEBAPP_URL}/api/ingest/batch",
            json={"games": games},
            timeout=_BATCH_TIMEOUT,
        ) as resp:
            resp.raise_for_status()
            data = await resp.json()
            return data["results"]
```

- [ ] **Step 4: Run — all 4 tests should pass**

```bash
cd scraper && python -m pytest tests/test_webapp_client.py -v
```
Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add scraper/webapp_client.py scraper/tests/test_webapp_client.py
git commit -m "feat: add webapp_client with filter_new and post_batch"
```

---

### Task 8: Simplify main.py

**Files:**
- Modify: `scraper/main.py`
- Create: `scraper/tests/test_main.py`

- [ ] **Step 1: Write failing tests**

Create `scraper/tests/test_main.py`:

```python
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import pytest
from unittest.mock import patch, AsyncMock
import main


@pytest.mark.asyncio
async def test_send_batches_chunks_into_groups_of_10():
    calls = []

    async def fake_post_batch(games):
        calls.append(len(games))
        return [{"object_id": g["object_id"], "ok": True, "error": None} for g in games]

    with patch.object(main, "post_batch", side_effect=fake_post_batch):
        games = [{"object_id": str(i)} for i in range(25)]
        await main._send_batches(games)

    assert calls == [10, 10, 5]


@pytest.mark.asyncio
async def test_send_batches_empty_does_nothing():
    called = False

    async def fake_post_batch(games):
        nonlocal called
        called = True
        return []

    with patch.object(main, "post_batch", side_effect=fake_post_batch):
        await main._send_batches([])

    assert not called


@pytest.mark.asyncio
async def test_send_batches_logs_failures(capsys):
    async def fake_post_batch(games):
        return [{"object_id": g["object_id"], "ok": False, "error": "thumbnail: all extensions failed"} for g in games]

    with patch.object(main, "post_batch", side_effect=fake_post_batch):
        await main._send_batches([{"object_id": "abc"}])

    captured = capsys.readouterr()
    assert "FAIL" in captured.out
    assert "abc" in captured.out
```

- [ ] **Step 2: Run — confirm tests fail**

```bash
cd scraper && python -m pytest tests/test_main.py -v
```
Expected: FAIL — `_send_batches` not found (old main.py has different structure).

- [ ] **Step 3: Replace main.py**

```python
import asyncio
import time

from config import CONCURRENCY, PAGE_DELAY
from gd_client import fetch_page, parse_hits, get_total_pages
from detail_fetcher import fetch_details_batch
from webapp_client import filter_new, post_batch

_BATCH_SIZE = 10


async def _send_batches(games: list[dict]) -> None:
    for i in range(0, len(games), _BATCH_SIZE):
        batch = games[i : i + _BATCH_SIZE]
        results = await post_batch(batch)
        for r in results:
            status = "OK" if r["ok"] else f"FAIL: {r.get('error', 'unknown')}"
            print(f"  [{status}] {r['object_id']}")


async def scrape_new_games() -> None:
    print("Starting GraphQL listing scrape...")
    data = fetch_page(1)
    total_pages = get_total_pages(data)
    print(f"Total pages: {total_pages}")

    for page in range(total_pages, 0, -1):
        print(f"Page {page}/{total_pages}...")
        try:
            data = fetch_page(page)
            hits = parse_hits(data)
        except ValueError as e:
            print(f"  [WARN] Skipping page {page}: {e}")
            time.sleep(PAGE_DELAY)
            continue

        object_ids = [g["object_id"] for g in hits]
        new_ids = set(await filter_new(object_ids))
        new_games = [g for g in hits if g["object_id"] in new_ids]

        if not new_games:
            print(f"  -> 0 new, {len(hits)} skipped")
            time.sleep(PAGE_DELAY)
            continue

        detail_results = await fetch_details_batch(new_games, CONCURRENCY)
        id_to_game = {g["object_id"]: g for g in new_games}
        full_games = [
            {**id_to_game[object_id], **detail}
            for object_id, detail in detail_results
            if detail and object_id in id_to_game
        ]

        skipped_detail = len(new_games) - len(full_games)
        print(
            f"  -> {len(full_games)} new, {len(hits) - len(new_games)} already known, "
            f"{skipped_detail} detail failed"
        )

        await _send_batches(full_games)
        time.sleep(PAGE_DELAY)

    print("Scrape complete.")


async def main() -> None:
    await scrape_new_games()


if __name__ == "__main__":
    asyncio.run(main())
```

- [ ] **Step 4: Run all scraper tests**

```bash
cd scraper && python -m pytest tests/ -v
```
Expected: All tests pass (test_gd_client, test_detail_fetcher, test_webapp_client, test_main).

- [ ] **Step 5: Run all webapp tests**

```bash
dotnet test webapp.Tests/
```
Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add scraper/main.py scraper/tests/test_main.py
git commit -m "feat: simplify main.py to use webapp ingest API"
```
