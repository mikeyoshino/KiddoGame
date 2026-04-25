# SEO Implementation Design

## Goal

Make kiddogame.net rank well on Google Thailand for Thai parents and kids searching for free online kids games. Every game page becomes a unique, indexable Thai-language landing page. The home page targets high-volume discovery keywords. Full option C: meta tags, Open Graph, JSON-LD structured data, sitemap, robots.txt, hreflang, and a visible Thai H1 on the home page.

## Target Audience & Keywords

**Two primary searchers:**
- Parents (30–45): safety-conscious, search for educational/appropriate content
- Kids (8–15): search for fun, free, no-download games

**Keyword clusters:**

| Cluster | Audience | Keywords |
|---|---|---|
| Free / no-download | Kids | `เกมฟรีไม่ต้องโหลด`, `เกมออนไลน์เล่นได้เลย` |
| Education / learning | Parents | `เกมการศึกษาเด็ก`, `เกมฝึกสมองเด็ก`, `เกมสอนภาษาอังกฤษ` |
| Safety | Parents | `เกมปลอดภัยสำหรับเด็ก` |
| Genre-specific | Both | `เกมปริศนาเด็ก`, `เกมแข่งรถออนไลน์`, `เกมแต่งตัวฟรี` |
| New / trending | Kids | `เกมใหม่ๆ ออนไลน์`, `เกมสนุกๆ ฟรี` |
| IO games | Older kids | `เกม io ออนไลน์ฟรี` |
| Brand | Both | `KiddoGame`, `KiddoGame เกมเด็ก` |

**Key insight:** keywords must appear throughout page content (H1, titles, descriptions, body text) — not only meta tags. The existing `description_th` column provides unique Thai body text per game, making each game page a distinct indexable document.

## Architecture

Five pieces, mirroring existing patterns (`Strings.cs`, `GenreTranslations.cs`):

```
webapp/Services/SeoMeta.cs                 ← Thai keyword strings + JSON-LD builders
webapp/Components/Shared/SeoHead.razor     ← shared component, renders all <head> tags
webapp/Components/Pages/Home.razor         ← add <SeoHead> + visible Thai <h1>
webapp/Components/Pages/GamePage.razor     ← add <SeoHead> with dynamic game data
webapp/wwwroot/robots.txt                  ← static file
webapp/Program.cs                          ← add /sitemap.xml minimal API endpoint
webapp/Components/App.razor                ← remove static <title> tag
```

## `SeoMeta.cs`

Static class in `Kiddo.Web.Services`. No DI needed — called directly like `Strings.cs`.

```csharp
public static class SeoMeta
{
    public const string BaseUrl    = "https://kiddogame.net";
    public const string SiteName   = "KiddoGame";
    public const string DefaultOgImage = "https://kiddogame.net/images/og-default.png";

    // Home page
    public const string HomeTitle =
        "KiddoGame - เกมออนไลน์สำหรับเด็กฟรี 2,000+ เกม ไม่ต้องดาวน์โหลด";
    public const string HomeDescription =
        "รวมเกมออนไลน์สำหรับเด็กฟรีกว่า 2,000 เกม เล่นได้เลยบนเบราว์เซอร์ " +
        "ไม่ต้องดาวน์โหลด ครอบคลุมทุกประเภท เกมการศึกษา เกมปริศนา เกมแข่งรถ " +
        "เกมแต่งตัว เกมทำอาหาร ปลอดภัยสำหรับเด็กทุกวัย";
    public const string HomeH1 =
        "เกมออนไลน์สำหรับเด็กฟรี เล่นได้เลยไม่ต้องดาวน์โหลด";

    // Game page (dynamic)
    public static string GameTitle(Game g) =>
        $"{g.Title} - เล่นออนไลน์ฟรี | เกมเด็ก {SiteName}";

    public static string GameDescription(Game g)
    {
        var text = !string.IsNullOrEmpty(g.DescriptionTh)
            ? g.DescriptionTh : g.Description ?? "";
        var snippet = text.Length > 100 ? text[..100] : text;
        return $"{snippet} เล่น {g.Title} ออนไลน์ฟรีได้เลยที่ {SiteName} ไม่ต้องดาวน์โหลด";
    }

    // JSON-LD
    public static string HomeJsonLd() { ... }    // WebSite + SearchAction
    public static string GameJsonLd(Game g, string url) { ... } // SoftwareApplication
}
```

### JSON-LD schemas

**Home — `WebSite` + `SearchAction`** (enables Google Sitelinks Searchbox):
```json
{
  "@context": "https://schema.org",
  "@type": "WebSite",
  "name": "KiddoGame",
  "url": "https://kiddogame.net",
  "description": "รวมเกมออนไลน์สำหรับเด็กฟรี...",
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
```

**Game page — `SoftwareApplication`** (triggers game rich results):
```json
{
  "@context": "https://schema.org",
  "@type": "SoftwareApplication",
  "applicationCategory": "GameApplication",
  "name": "[game.Title]",
  "description": "[Thai or English description]",
  "image": "[game.ThumbnailUrl]",
  "url": "[canonical URL]",
  "operatingSystem": "Web Browser",
  "inLanguage": "th",
  "offers": {
    "@type": "Offer",
    "price": "0",
    "priceCurrency": "THB"
  }
}
```

## `SeoHead.razor`

New file at `webapp/Components/Shared/SeoHead.razor`. Renders all head tags using Blazor's `<PageTitle>` and `<HeadContent>` (already wired via `<HeadOutlet />` in `App.razor`).

**Parameters:**
- `[Parameter, EditorRequired] string Title`
- `[Parameter, EditorRequired] string Description`
- `[Parameter, EditorRequired] string Canonical`
- `[Parameter] string OgType = "website"`
- `[Parameter] string? OgImage`
- `[Parameter] string? JsonLd`

**Renders:**
- `<PageTitle>` — replaces static `<title>` in App.razor
- `<meta name="description">`
- `<link rel="canonical">`
- `og:type`, `og:title`, `og:description`, `og:url`, `og:site_name`, `og:locale`, `og:image`
- `twitter:card`, `twitter:title`, `twitter:description`, `twitter:image`
- `hreflang` links: `th`, `en`, `x-default` (all pointing to same canonical — same URL serves both languages via switcher)
- `<script type="application/ld+json">` when `JsonLd` is provided

## Page Changes

### `App.razor`
Remove: `<title>KiddoGame</title>` — `<PageTitle>` via `<HeadOutlet />` now handles all titles.

### `Home.razor`
- Add `<SeoHead Title="SeoMeta.HomeTitle" Description="SeoMeta.HomeDescription" Canonical="@(SeoMeta.BaseUrl + "/")" JsonLd="@SeoMeta.HomeJsonLd()" />` at the top
- Add a visible Thai H1 above the search bar: `<h1 class="sr-only">@SeoMeta.HomeH1</h1>` — screen-reader only styling so layout is unchanged but Google sees the keyword-rich H1

### `GamePage.razor`
- Add `<SeoHead>` after the game loads (inside the `else` block), using:
  - `Title="SeoMeta.GameTitle(_game)"`
  - `Description="SeoMeta.GameDescription(_game)"`
  - `Canonical="@(SeoMeta.BaseUrl + "/games/" + _game.Slug)"`
  - `OgType="article"`
  - `OgImage="@(_game.ThumbnailUrl ?? SeoMeta.DefaultOgImage)"`
  - `JsonLd="@SeoMeta.GameJsonLd(_game, SeoMeta.BaseUrl + "/games/" + _game.Slug)"`
- Show a fallback `<SeoHead>` with generic title during loading state too

## Sitemap

**Endpoint:** `GET /sitemap.xml` added to `Program.cs` as a minimal API endpoint.

Fetches all game slugs from Supabase (`games?select=slug&status=eq.done`), builds XML, returns with `Content-Type: application/xml`.

Includes:
- Home page: `priority=1.0`, `changefreq=daily`
- Each game page: `priority=0.8`, `changefreq=monthly`

Uses the existing `IHttpClientFactory` / `GameService` HTTP client registered in DI — no new HTTP client needed.

## `robots.txt`

Static file at `webapp/wwwroot/robots.txt`:
```
User-agent: *
Allow: /
Disallow: /Error
Sitemap: https://kiddogame.net/sitemap.xml
```

Served automatically by `app.UseStaticFiles()` already in `Program.cs`.

## Error Handling

- `SeoMeta.GameDescription()` — if both `DescriptionTh` and `Description` are null/empty, falls back to `$"เล่น {g.Title} ออนไลน์ฟรีได้เลยที่ {SiteName} ไม่ต้องดาวน์โหลด"` (still keyword-rich)
- Sitemap endpoint — if Supabase is unreachable, returns HTTP 503 (not a broken XML response)
- `SeoHead` during game loading — show a minimal title (`"KiddoGame - เกมเด็กออนไลน์ฟรี"`) so the tab title isn't blank while the game data loads

## Testing

- Title tag and meta description are correct on home page (Thai, keyword-rich)
- Title tag on game page includes game title + Thai suffix
- Open Graph tags present and correct on both pages (verified via Facebook Sharing Debugger or curl)
- JSON-LD validates in Google's Rich Results Test
- `/sitemap.xml` returns valid XML with home + game URLs
- `/robots.txt` accessible and contains Sitemap directive
- `<h1>` present on home page in DOM
- Switching to English mode does not break any head tags (they stay Thai — primary SEO target)
