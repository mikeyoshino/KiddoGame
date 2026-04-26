# About Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Thai-language About page at `/about` with mission text, feature cards, genre highlights, and how-to-play steps, linked from both the desktop and mobile nav.

**Architecture:** Static Razor component (`About.razor`) with no DB calls. All bilingual content is inline using `LangSvc.Current == Lang.Thai` guards. `<SeoHead>` renders the Thai-targeted head tags. Nav links added to `TopNav.razor` (desktop) and `MobileNav.razor` (mobile). `/about` added to the sitemap in `Program.cs`.

**Tech Stack:** Blazor Server (.NET 8), Tailwind CSS, xUnit (existing), `Kiddo.Web.Services.LanguageService`

---

### Task 1: Add `nav_about` to Strings.cs

**Files:**
- Modify: `webapp/Services/Strings.cs`

No unit test needed — `Strings.Get` is a dictionary lookup already covered by the existing pattern; adding a new key follows the identical path.

- [ ] **Step 1: Add the entry**

In `webapp/Services/Strings.cs`, add one line to `_table` after the `"genre"` entry:

```csharp
["nav_about"] = ("เกี่ยวกับเรา", "About"),
```

The full bottom of the dictionary should look like:

```csharp
        ["filters"]            = ("ตัวกรอง",                              "Filters"),
        ["genre"]              = ("ประเภท",                               "Genre"),
        ["nav_about"]          = ("เกี่ยวกับเรา",                         "About"),
    };
```

- [ ] **Step 2: Verify build passes**

```bash
dotnet build webapp/
```

Expected: Build succeeded, 0 errors

- [ ] **Step 3: Commit**

```bash
git add webapp/Services/Strings.cs
git commit -m "feat: add nav_about string for About page nav link"
```

---

### Task 2: Create About.razor

**Files:**
- Create: `webapp/Components/Pages/About.razor`

No unit tests for Razor components — verify with build + manual browser check.

- [ ] **Step 1: Create the file**

```razor
@* webapp/Components/Pages/About.razor *@
@page "/about"
@rendermode InteractiveServer
@inject LanguageService LangSvc
@implements IDisposable

<SeoHead Title="เกี่ยวกับ KiddoGame - เกมออนไลน์สำหรับเด็กฟรี ปลอดภัย ไม่ต้องดาวน์โหลด"
         Description="KiddoGame รวมเกมออนไลน์ฟรีสำหรับเด็กกว่า 2,000 เกม ปลอดภัย ไม่ต้องดาวน์โหลด เล่นได้ทันทีบนเบราว์เซอร์ เหมาะสำหรับเด็กทุกวัย"
         Canonical="https://kiddogame.net/about"
         JsonLd="@_jsonLd" />

<div class="max-w-2xl mx-auto">

    <h1 class="text-3xl font-bold mb-6">
        @(IsThai ? "เกี่ยวกับ KiddoGame" : "About KiddoGame")
    </h1>

    @if (IsThai)
    {
        <p class="text-slate-600 leading-relaxed mb-4">
            KiddoGame คือแหล่งรวมเกมออนไลน์สำหรับเด็กฟรีที่ใหญ่ที่สุด เราเชื่อว่าเด็กทุกคนควรมีสิทธิ์เข้าถึงเกมที่สนุก ปลอดภัย และเสริมพัฒนาการ โดยไม่มีค่าใช้จ่ายใดๆ
        </p>
        <p class="text-slate-600 leading-relaxed mb-8">
            เราก่อตั้ง KiddoGame ขึ้นเพราะเชื่อว่าการเล่นเกมที่ดีช่วยพัฒนาความคิดสร้างสรรค์ ทักษะการแก้ปัญหา และความสนุกสนานของเด็กๆ ทุกเกมบน KiddoGame ผ่านการคัดสรรให้เหมาะสมกับเด็กทุกวัย
        </p>
    }
    else
    {
        <p class="text-slate-600 leading-relaxed mb-4">
            KiddoGame is your go-to destination for free online kids games. We believe every child deserves access to fun, safe, and enriching games — completely free.
        </p>
        <p class="text-slate-600 leading-relaxed mb-8">
            We built KiddoGame because great games help kids develop creativity, problem-solving, and joy. Every game on KiddoGame is carefully chosen to be age-appropriate and safe.
        </p>
    }

    <div class="grid grid-cols-2 gap-4 mb-10">
        @foreach (var card in _cards)
        {
            <div class="bg-indigo-50 rounded-2xl p-6 flex flex-col gap-2">
                <span class="text-4xl">@card.Icon</span>
                <p class="font-bold text-indigo-700">@(IsThai ? card.HeadingTh : card.HeadingEn)</p>
                <p class="text-sm text-slate-500">@(IsThai ? card.SubTh : card.SubEn)</p>
            </div>
        }
    </div>

    <h2 class="text-xl font-bold mt-10 mb-3">
        @(IsThai ? "เกมครอบคลุมทุกประเภท" : "Games for Every Interest")
    </h2>

    @if (IsThai)
    {
        <p class="text-slate-600 leading-relaxed mb-8">
            เรามีเกมครอบคลุมทุกประเภท ไม่ว่าจะเป็นเกมปริศนาฝึกสมอง เกมแข่งรถมันส์ๆ เกมการศึกษาสอนภาษาอังกฤษ เกมแต่งตัว เกมทำอาหาร เกมแคชวล เกมไอโอ และอีกมากมาย รับรองว่ามีเกมที่ถูกใจเด็กทุกคนอย่างแน่นอน
        </p>
    }
    else
    {
        <p class="text-slate-600 leading-relaxed mb-8">
            From brain-teasing puzzles and racing games to educational titles, dress-up, cooking, casual, and IO games — there's something for every kid on KiddoGame.
        </p>
    }

    <h2 class="text-xl font-bold mt-10 mb-3">
        @(IsThai ? "วิธีเริ่มเล่น" : "How to Start Playing")
    </h2>

    @if (IsThai)
    {
        <ol class="list-decimal list-inside space-y-2 text-slate-600 mb-10">
            <li>เลือกเกมที่ชอบจากหน้าแรก</li>
            <li>กดที่เกมเพื่อเข้าสู่หน้าเกม</li>
            <li>กดเล่นได้เลย ไม่ต้องสมัครสมาชิก ไม่ต้องดาวน์โหลด</li>
        </ol>
    }
    else
    {
        <ol class="list-decimal list-inside space-y-2 text-slate-600 mb-10">
            <li>Browse and pick a game from the home page</li>
            <li>Click the game to open its page</li>
            <li>Hit play — no sign-up, no download needed</li>
        </ol>
    }

</div>

@code {
    private bool IsThai => LangSvc.Current == Lang.Thai;

    private const string _jsonLd = """
        {
          "@context": "https://schema.org",
          "@type": "Organization",
          "name": "KiddoGame",
          "url": "https://kiddogame.net",
          "description": "รวมเกมออนไลน์ฟรีสำหรับเด็กกว่า 2,000 เกม ปลอดภัย ไม่ต้องดาวน์โหลด"
        }
        """;

    private record Card(string Icon, string HeadingTh, string SubTh, string HeadingEn, string SubEn);

    private static readonly Card[] _cards =
    [
        new("🎮", "2,000+ เกม",         "รวมเกมหลากหลายทุกประเภท",  "2,000+ Games",  "Every genre covered"),
        new("🆓", "ฟรีทั้งหมด",         "ไม่มีค่าใช้จ่ายใดๆ",        "100% Free",     "No cost, ever"),
        new("🛡️", "ปลอดภัยสำหรับเด็ก", "คัดสรรเนื้อหาเหมาะสม",      "Safe for Kids", "Carefully curated content"),
        new("📱", "ไม่ต้องดาวน์โหลด",   "เล่นได้เลยบนเบราว์เซอร์",  "No Download",   "Play right in your browser"),
    ];

    protected override void OnInitialized() =>
        LangSvc.OnChanged += OnLanguageChanged;

    private void OnLanguageChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() =>
        LangSvc.OnChanged -= OnLanguageChanged;
}
```

- [ ] **Step 2: Verify build passes**

```bash
dotnet build webapp/
```

Expected: Build succeeded, 0 errors

- [ ] **Step 3: Commit**

```bash
git add webapp/Components/Pages/About.razor
git commit -m "feat: add About page with Thai SEO content and feature cards"
```

---

### Task 3: Update TopNav.razor — add About link

**Files:**
- Modify: `webapp/Components/Layout/TopNav.razor`

- [ ] **Step 1: Add the About link to the nav**

In `webapp/Components/Layout/TopNav.razor`, add the About `<a>` tag after the Favorites link, inside `<nav>`:

```razor
<nav class="flex items-center gap-1">
    <a href="/"
       class="px-4 py-2 rounded-xl text-sm font-medium transition-colors
              @(IsHome ? "bg-indigo-100 text-indigo-700" : "text-slate-500 hover:bg-slate-100 hover:text-slate-700")">
        @Strings.Get(LangSvc.Current, "nav_home")
    </a>
    <a href="/favorites"
       class="px-4 py-2 rounded-xl text-sm font-medium transition-colors
              @(IsFavorites ? "bg-indigo-100 text-indigo-700" : "text-slate-500 hover:bg-slate-100 hover:text-slate-700")">
        @Strings.Get(LangSvc.Current, "favorites")
    </a>
    <a href="/about"
       class="px-4 py-2 rounded-xl text-sm font-medium transition-colors
              @(IsAbout ? "bg-indigo-100 text-indigo-700" : "text-slate-500 hover:bg-slate-100 hover:text-slate-700")">
        @Strings.Get(LangSvc.Current, "nav_about")
    </a>
</nav>
```

- [ ] **Step 2: Add IsAbout computed property to @code**

Add after the `IsFavorites` property in the `@code` block:

```csharp
private bool IsAbout =>
    Nav.Uri.TrimEnd('/').Equals(Nav.BaseUri.TrimEnd('/') + "about", StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 3: Verify build passes**

```bash
dotnet build webapp/
```

Expected: Build succeeded, 0 errors

- [ ] **Step 4: Commit**

```bash
git add webapp/Components/Layout/TopNav.razor
git commit -m "feat: add About link to desktop TopNav"
```

---

### Task 4: Update MobileNav.razor — replace Search tab with About

**Files:**
- Modify: `webapp/Components/Layout/MobileNav.razor`

The third tab currently points to `/` with a search icon and hardcoded Thai label `ค้นหา` — it's a dead placeholder. Replace it with the About tab.

- [ ] **Step 1: Replace the Search tab with About**

In `webapp/Components/Layout/MobileNav.razor`, replace the third `<a>` tag (the one with `href="/"` and `ค้นหา`):

**Remove:**
```razor
    <a href="/" class="flex flex-col items-center text-slate-400">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
        </svg>
        <span class="text-xs mt-1 font-light">ค้นหา</span>
    </a>
```

**Add:**
```razor
    <a href="/about" class="flex flex-col items-center @(IsAbout ? "text-indigo-600" : "text-slate-400")">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="m11.25 11.25.041-.02a.75.75 0 0 1 1.063.852l-.708 2.836a.75.75 0 0 0 1.063.853l.041-.021M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9-3.75h.008v.008H12V8.25Z" />
        </svg>
        <span class="text-xs mt-1 font-light">@Strings.Get(LangSvc.Current, "nav_about")</span>
    </a>
```

- [ ] **Step 2: Add IsAbout computed property to @code**

Add after the `IsFavorites` property in the `@code` block:

```csharp
private bool IsAbout =>
    Nav.Uri.TrimEnd('/').Equals(Nav.BaseUri.TrimEnd('/') + "about", StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 3: Verify build passes**

```bash
dotnet build webapp/
```

Expected: Build succeeded, 0 errors

- [ ] **Step 4: Commit**

```bash
git add webapp/Components/Layout/MobileNav.razor
git commit -m "feat: add About tab to mobile nav, remove dead Search placeholder"
```

---

### Task 5: Add /about to sitemap in Program.cs

**Files:**
- Modify: `webapp/Program.cs`

- [ ] **Step 1: Add the About URL after the home page entry**

In `webapp/Program.cs`, inside the `/sitemap.xml` handler, add the About URL block immediately after the home page `</url>` closing tag:

```csharp
    sb.AppendLine("  <url>");
    sb.AppendLine("    <loc>https://kiddogame.net/</loc>");
    sb.AppendLine("    <changefreq>daily</changefreq>");
    sb.AppendLine("    <priority>1.0</priority>");
    sb.AppendLine("  </url>");

    sb.AppendLine("  <url>");
    sb.AppendLine("    <loc>https://kiddogame.net/about</loc>");
    sb.AppendLine("    <changefreq>monthly</changefreq>");
    sb.AppendLine("    <priority>0.9</priority>");
    sb.AppendLine("  </url>");

    foreach (var slug in slugs)
```

- [ ] **Step 2: Verify build passes**

```bash
dotnet build webapp/
```

Expected: Build succeeded, 0 errors

- [ ] **Step 3: Commit**

```bash
git add webapp/Program.cs
git commit -m "feat: add /about to sitemap.xml endpoint"
```

---

### Task 6: Run tests and verify

- [ ] **Step 1: Run full test suite**

```bash
dotnet test webapp.Tests/
```

Expected: All 39 tests pass (no new unit tests were needed — no pure logic was added)

- [ ] **Step 2: Start the dev server and check the About page**

```bash
dotnet run --project webapp/
```

Open `http://localhost:5001/about` in a browser and verify:
- Thai H1 "เกี่ยวกับ KiddoGame" is visible
- 4 feature cards render in a 2×2 grid
- Genre highlights paragraph is present
- How to play steps are present
- Switch to English via language switcher — all text switches to English

- [ ] **Step 3: Check nav active state**

- Desktop: TopNav shows "เกี่ยวกับเรา" highlighted (indigo background) when on `/about`
- Mobile: third bottom tab shows info icon highlighted when on `/about`
- Navigate away — highlight disappears

- [ ] **Step 4: Verify page title via curl**

```bash
curl -s http://localhost:5001/about | grep -o '<title>[^<]*</title>'
```

Expected:
```
<title>เกี่ยวกับ KiddoGame - เกมออนไลน์สำหรับเด็กฟรี ปลอดภัย ไม่ต้องดาวน์โหลด</title>
```
