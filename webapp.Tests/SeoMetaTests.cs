using Kiddo.Web.Models;
using Kiddo.Web.Services;

namespace Kiddo.Web.Tests;

public class SeoMetaTests
{
    private static Game MakeGame(
        string title   = "Bubble Pop",
        string slug    = "bubble-pop",
        string? descTh = "เกมสนุกสำหรับเด็ก",
        string? descEn = "A fun bubble game") => new()
    {
        Title = title, Slug = slug,
        DescriptionTh = descTh, Description = descEn,
        ObjectId = "abc123"
    };

    [Fact]
    public void GameTitle_ReturnsThaiSuffix()
    {
        var result = SeoMeta.GameTitle(MakeGame("Bubble Pop"));
        Assert.Equal("Bubble Pop - เล่นออนไลน์ฟรี | เกมเด็ก KiddoGame", result);
    }

    [Fact]
    public void GameDescription_PrefersThaiDescription()
    {
        var result = SeoMeta.GameDescription(MakeGame(descTh: "เกมสนุกสำหรับเด็ก"));
        Assert.StartsWith("เกมสนุกสำหรับเด็ก", result);
        Assert.Contains("ไม่ต้องดาวน์โหลด", result);
    }

    [Fact]
    public void GameDescription_FallsBackToEnglish_WhenNoThai()
    {
        var result = SeoMeta.GameDescription(MakeGame(descTh: null, descEn: "A fun bubble game"));
        Assert.StartsWith("A fun bubble game", result);
        Assert.Contains("ไม่ต้องดาวน์โหลด", result);
    }

    [Fact]
    public void GameDescription_FallsBackToKeywords_WhenNoBothDescriptions()
    {
        var result = SeoMeta.GameDescription(MakeGame("My Game", descTh: null, descEn: null));
        Assert.Contains("My Game", result);
        Assert.Contains("ไม่ต้องดาวน์โหลด", result);
    }

    [Fact]
    public void GameDescription_TruncatesLongDesc_At100Chars()
    {
        var longDesc = new string('ก', 200);
        var result = SeoMeta.GameDescription(MakeGame(descTh: longDesc));
        Assert.StartsWith(new string('ก', 100) + " เล่น", result);
    }

    [Fact]
    public void HomeJsonLd_ContainsWebSiteSchemaAndSearchAction()
    {
        var result = SeoMeta.HomeJsonLd();
        Assert.Contains("\"@type\": \"WebSite\"", result);
        Assert.Contains("kiddogame.net", result);
        Assert.Contains("SearchAction", result);
    }

    [Fact]
    public void GameJsonLd_ContainsSoftwareApplicationAndFreeOffer()
    {
        var game = MakeGame("Bubble Pop", slug: "bubble-pop");
        var result = SeoMeta.GameJsonLd(game, "https://kiddogame.net/games/bubble-pop");
        Assert.Contains("\"@type\": \"SoftwareApplication\"", result);
        Assert.Contains("Bubble Pop", result);
        Assert.Contains("https://kiddogame.net/games/bubble-pop", result);
        Assert.Contains("\"price\": \"0\"", result);
    }
}
