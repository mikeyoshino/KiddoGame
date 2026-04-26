using System.Text.Json;
using Kiddo.Web.Models;

namespace Kiddo.Web.Tests;

public class GameTests
{
    [Fact]
    public void GameUrl_ReturnsCorrectUrl()
    {
        var game = new Game { ObjectId = "f078134f39634ca78dcd4a8479a314a2", Slug = "test-game" };
        Assert.Equal(
            "https://html5.gamedistribution.com/f078134f39634ca78dcd4a8479a314a2/?gd_sdk_referrer_url=https://kiddogame.net/games/test-game/",
            game.GameUrl);
    }

    [Fact]
    public void Game_DeserializesFromSnakeCaseJson()
    {
        var json = """
            {
                "id": "abc",
                "object_id": "f078134f39634ca78dcd4a8479a314a2",
                "slug": "67-clicker",
                "title": "67 Clicker",
                "company": "Miomi",
                "thumbnail_url": "https://img.gamedistribution.com/f078134f39634ca78dcd4a8479a314a2-512x384.jpg",
                "description": "A great game",
                "instruction": "Click to play",
                "categories": ["Casual", "Agility"],
                "tags": ["clicker"],
                "languages": ["English"],
                "gender": ["Male"],
                "age_group": ["Kids"],
                "status": "done",
                "view_count": 42,
                "created_at": "2026-04-24T00:00:00Z"
            }
            """;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
        };

        var game = JsonSerializer.Deserialize<Game>(json, options);

        Assert.NotNull(game);
        Assert.Equal("f078134f39634ca78dcd4a8479a314a2", game.ObjectId);
        Assert.Equal("67-clicker", game.Slug);
        Assert.Equal("67 Clicker", game.Title);
        Assert.Equal(new[] { "Casual", "Agility" }, game.Categories);
        Assert.Equal(42, game.ViewCount);
    }
}
