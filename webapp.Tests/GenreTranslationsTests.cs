using Kiddo.Web.Models;
using Kiddo.Web.Services;

namespace Kiddo.Web.Tests;

public class GenreTranslationsTests
{
    [Fact]
    public void Translate_ThaiMode_Casual_ReturnsThai()
    {
        var result = GenreTranslations.Translate("Casual", Lang.Thai);
        Assert.Equal("แคชวล", result);
    }

    [Fact]
    public void Translate_ThaiMode_Puzzle_ReturnsThai()
    {
        var result = GenreTranslations.Translate("Puzzle", Lang.Thai);
        Assert.Equal("ปริศนา", result);
    }

    [Fact]
    public void Translate_ThaiMode_RacingAndDriving_ReturnsThai()
    {
        var result = GenreTranslations.Translate("Racing & Driving", Lang.Thai);
        Assert.Equal("แข่งรถ", result);
    }

    [Fact]
    public void Translate_ThaiMode_DotIO_ReturnsThai()
    {
        var result = GenreTranslations.Translate(".IO", Lang.Thai);
        Assert.Equal("ไอโอ", result);
    }

    [Fact]
    public void Translate_ThaiMode_UnknownGenre_ReturnsEnglishKey()
    {
        var result = GenreTranslations.Translate("UnknownGenre", Lang.Thai);
        Assert.Equal("UnknownGenre", result);
    }

    [Fact]
    public void Translate_EnglishMode_KnownGenre_ReturnsEnglishKey()
    {
        var result = GenreTranslations.Translate("Casual", Lang.English);
        Assert.Equal("Casual", result);
    }

    [Fact]
    public void Translate_EnglishMode_UnknownGenre_ReturnsEnglishKey()
    {
        var result = GenreTranslations.Translate("Whatever", Lang.English);
        Assert.Equal("Whatever", result);
    }
}
