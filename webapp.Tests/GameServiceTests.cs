using System.Net;
using System.Text;
using Kiddo.Web.Services;

namespace Kiddo.Web.Tests;

public class GameServiceTests
{
    private static HttpClient MakeClient(string json, string contentRange = "0-0/0")
    {
        var handler = new FakeHandler(json, contentRange);
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://fake.supabase.co/rest/v1/")
        };
    }

    [Fact]
    public void ParseTotal_ExtractsCountFromContentRange()
    {
        Assert.Equal(21344, GameService.ParseTotal("0-29/21344"));
        Assert.Equal(0, GameService.ParseTotal(null));
        Assert.Equal(0, GameService.ParseTotal("bad"));
    }

    [Fact]
    public async Task GetGamesAsync_ReturnsGamesAndTotal()
    {
        var json = """
            [
                {"id":"1","object_id":"abc","slug":"test-game","title":"Test Game",
                 "company":"Test Co","thumbnail_url":null,"description":null,
                 "instruction":null,"categories":["Casual"],"tags":[],"languages":[],
                 "gender":[],"age_group":[],"status":"done","view_count":0,
                 "created_at":"2026-04-24T00:00:00Z"}
            ]
            """;

        var client = MakeClient(json, "0-0/1");
        var service = new GameService(client);

        var (games, total) = await service.GetGamesAsync(1);

        Assert.Single(games);
        Assert.Equal("Test Game", games[0].Title);
        Assert.Equal(1, total);
    }

    [Fact]
    public async Task GetGameBySlugAsync_ReturnsGame()
    {
        var json = """
            [{"id":"1","object_id":"abc","slug":"test-game","title":"Test Game",
              "company":null,"thumbnail_url":null,"description":null,"instruction":null,
              "categories":[],"tags":[],"languages":[],"gender":[],"age_group":[],
              "status":"done","view_count":0,"created_at":"2026-04-24T00:00:00Z"}]
            """;

        var client = MakeClient(json);
        var service = new GameService(client);

        var game = await service.GetGameBySlugAsync("test-game");

        Assert.NotNull(game);
        Assert.Equal("test-game", game.Slug);
    }

    [Fact]
    public async Task GetGameBySlugAsync_ReturnsNullWhenNotFound()
    {
        var client = MakeClient("[]");
        var service = new GameService(client);

        var game = await service.GetGameBySlugAsync("nonexistent");

        Assert.Null(game);
    }

    [Fact]
    public async Task GetSimilarGamesAsync_ReturnsSimilarGames()
    {
        var json = """
            [{"id":"2","object_id":"def","slug":"similar-game","title":"Similar Game",
              "company":null,"thumbnail_url":null,"description":null,"instruction":null,
              "categories":["Casual"],"tags":[],"languages":[],"gender":[],"age_group":[],
              "status":"done","view_count":0,"created_at":"2026-04-24T00:00:00Z"}]
            """;

        var client = MakeClient(json);
        var service = new GameService(client);

        var similar = await service.GetSimilarGamesAsync(["Casual"], "test-game");

        Assert.Single(similar);
        Assert.Equal("Similar Game", similar[0].Title);
    }

    [Fact]
    public async Task GetCategoriesAsync_ReturnsCategories()
    {
        var json = """[{"category":"Casual"},{"category":"Agility"}]""";
        var client = MakeClient(json);
        var service = new GameService(client);

        var categories = await service.GetCategoriesAsync();

        Assert.Equal(new[] { "Casual", "Agility" }, categories);
    }
}

internal class FakeHandler(string json, string contentRange = "0-0/0") : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        response.Content.Headers.TryAddWithoutValidation("Content-Range", contentRange);
        return Task.FromResult(response);
    }
}
