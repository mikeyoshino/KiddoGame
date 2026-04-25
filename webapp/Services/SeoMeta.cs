using System.Text.Encodings.Web;
using System.Text.Json;
using Kiddo.Web.Models;

namespace Kiddo.Web.Services;

public static class SeoMeta
{
    public const string BaseUrl        = "https://kiddogame.net";
    public const string SiteName       = "KiddoGame";
    public const string DefaultOgImage = "https://kiddogame.net/images/og-default.png";

    public const string HomeTitle =
        "KiddoGame - เกมออนไลน์สำหรับเด็กฟรี 2,000+ เกม ไม่ต้องดาวน์โหลด";

    public const string HomeDescription =
        "รวมเกมออนไลน์สำหรับเด็กฟรีกว่า 2,000 เกม เล่นได้เลยบนเบราว์เซอร์ " +
        "ไม่ต้องดาวน์โหลด ครอบคลุมทุกประเภท เกมการศึกษา เกมปริศนา เกมแข่งรถ " +
        "เกมแต่งตัว เกมทำอาหาร ปลอดภัยสำหรับเด็กทุกวัย";

    public const string HomeH1 =
        "เกมออนไลน์สำหรับเด็กฟรี เล่นได้เลยไม่ต้องดาวน์โหลด";

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string Esc(string v) =>
        JsonSerializer.Serialize(v, _jsonOpts)[1..^1];

    public static string GameTitle(Game g) =>
        $"{g.Title} - เล่นออนไลน์ฟรี | เกมเด็ก {SiteName}";

    public static string GameDescription(Game g)
    {
        var text = !string.IsNullOrEmpty(g.DescriptionTh) ? g.DescriptionTh
                 : !string.IsNullOrEmpty(g.Description)   ? g.Description
                 : "";
        var snippet = text.Length > 100 ? text[..100] : text;
        return string.IsNullOrEmpty(snippet)
            ? $"เล่น {g.Title} ออนไลน์ฟรีได้เลยที่ {SiteName} ไม่ต้องดาวน์โหลด"
            : $"{snippet} เล่น {g.Title} ออนไลน์ฟรีได้เลยที่ {SiteName} ไม่ต้องดาวน์โหลด";
    }

    public static string HomeJsonLd() => """
        {
          "@context": "https://schema.org",
          "@type": "WebSite",
          "name": "KiddoGame",
          "url": "https://kiddogame.net",
          "description": "รวมเกมออนไลน์สำหรับเด็กฟรีกว่า 2,000 เกม เล่นได้เลยบนเบราว์เซอร์ ไม่ต้องดาวน์โหลด",
          "inLanguage": "th",
          "potentialAction": {
            "@type": "SearchAction",
            "target": {
              "@type": "EntryPoint",
              "urlTemplate": "https://kiddogame.net/?search={search_term_string}"
            },
            "query-input": "required name=search_term_string"
          }
        }
        """;

    public static string GameJsonLd(Game g, string url)
    {
        var desc  = !string.IsNullOrEmpty(g.DescriptionTh) ? g.DescriptionTh
                  : g.Description ?? g.Title;
        var image = g.ThumbnailUrl ?? DefaultOgImage;
        return $$"""
            {
              "@context": "https://schema.org",
              "@type": "SoftwareApplication",
              "applicationCategory": "GameApplication",
              "name": "{{Esc(g.Title)}}",
              "description": "{{Esc(desc)}}",
              "image": "{{image}}",
              "url": "{{url}}",
              "operatingSystem": "Web Browser",
              "inLanguage": "th",
              "offers": {
                "@type": "Offer",
                "price": "0",
                "priceCurrency": "THB"
              }
            }
            """;
    }
}
