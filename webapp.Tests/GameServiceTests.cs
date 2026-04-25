using System.Net;
using System.Text;
using Kiddo.Web.Models;
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
    public void BuildGamesUrl_NoFilter_BuildsBasicUrl()
    {
        var url = GameService.BuildGamesUrl(1, 30, null, null);
        Assert.Equal("games?select=*&order=created_at.desc&offset=0&limit=30", url);
    }

    [Fact]
    public void BuildGamesUrl_PageTwo_CorrectOffset()
    {
        var url = GameService.BuildGamesUrl(2, 30, null, null);
        Assert.Contains("offset=30", url);
    }

    [Fact]
    public void BuildGamesUrl_SingleCategory_UsesOvOperator()
    {
        var url = GameService.BuildGamesUrl(1, 30, new HashSet<string> { "Casual" }, null);
        Assert.Contains("&categories=ov.%7BCasual%7D", url);
    }

    [Fact]
    public void BuildGamesUrl_MultipleCategories_IncludesBothNames()
    {
        var url = GameService.BuildGamesUrl(1, 30, new HashSet<string> { "Casual", "Puzzle" }, null);
        Assert.Contains("&categories=ov.%7B", url);
        Assert.Contains("Casual", url);
        Assert.Contains("Puzzle", url);
    }

    [Fact]
    public void BuildGamesUrl_EmptyCategories_NoCategoryFilter()
    {
        var url = GameService.BuildGamesUrl(1, 30, new HashSet<string>(), null);
        Assert.DoesNotContain("categories", url);
    }

    [Fact]
    public void BuildGamesUrl_WithSearch_SearchesTitleAndDescriptionEn()
    {
        var url = GameService.BuildGamesUrl(1, 30, null, "mario", Lang.English);
        Assert.Contains("title.ilike.*mario*", url);
        Assert.Contains("description.ilike.*mario*", url);
        Assert.DoesNotContain("description_th", url);
    }

    [Fact]
    public void BuildGamesUrl_WithSearch_ThaiSearchesDescriptionTh()
    {
        var url = GameService.BuildGamesUrl(1, 30, null, "mario", Lang.Thai);
        Assert.Contains("title.ilike.*mario*", url);
        Assert.Contains("description_th.ilike.*mario*", url);
        Assert.DoesNotContain("description.ilike.*mario*", url);
    }

    [Fact]
    public async Task GetCategoriesAsync_ReturnsGenresWithCounts()
    {
        var json = """[{"category":"Casual","count":5071},{"category":"Puzzle","count":3660}]""";
        var client = MakeClient(json);
        var service = new GameService(client);

        var genres = await service.GetCategoriesAsync();

        Assert.Equal(2, genres.Count);
        Assert.Equal(("Casual", 5071), genres[0]);
        Assert.Equal(("Puzzle", 3660), genres[1]);
    }

    [Fact]
    public async Task GetGamesBySlugsAsync_ReturnsEmptyForNoSlugs()
    {
        var client = MakeClient("[]");
        var service = new GameService(client);

        var result = await service.GetGamesBySlugsAsync([]);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetGamesBySlugsAsync_ReturnsMatchingGames()
    {
        var json = """
            [{"id":"1","object_id":"abc","slug":"game-1","title":"Game One",
              "company":null,"thumbnail_url":null,"description":null,"instruction":null,
              "categories":[],"tags":[],"languages":[],"gender":[],"age_group":[],
              "status":"done","view_count":0,"created_at":"2026-04-24T00:00:00Z"}]
            """;
        var client = MakeClient(json);
        var service = new GameService(client);

        var result = await service.GetGamesBySlugsAsync(["game-1"]);

        Assert.Single(result);
        Assert.Equal("game-1", result[0].Slug);
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
