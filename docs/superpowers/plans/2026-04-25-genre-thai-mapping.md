# Genre Thai Mapping Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Display game genres in Thai when Thai mode is active, with automatic English fallback for unmapped genres, while always using English keys for DB queries.

**Architecture:** A new static `GenreTranslations` class (mirroring `Strings.cs`) holds the English→Thai map and a `Translate()` method. The three Razor components that display genres call `Translate()` for display only; all selection state and API calls continue to use English strings unchanged.

**Tech Stack:** C# / .NET 8 Blazor Server, xUnit tests in `webapp.Tests/`

---

### Task 1: Add `GenreTranslations` static class (TDD)

**Files:**
- Create: `webapp/Services/GenreTranslations.cs`
- Create: `webapp.Tests/GenreTranslationsTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `webapp.Tests/GenreTranslationsTests.cs`:

```csharp
using Kiddo.Web.Models;
using Kiddo.Web.Services;

namespace Kiddo.Web.Tests;

public class GenreTranslationsTests
{
    [Fact]
    public void Translate_ThaiMode_KnownGenre_ReturnsThai()
    {
        var result = GenreTranslations.Translate("Action", Lang.Thai);
        Assert.Equal("แอ็กชัน", result);
    }

    [Fact]
    public void Translate_ThaiMode_KnownGenre_Puzzle_ReturnsThai()
    {
        var result = GenreTranslations.Translate("Puzzle", Lang.Thai);
        Assert.Equal("ปริศนา", result);
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
        var result = GenreTranslations.Translate("Action", Lang.English);
        Assert.Equal("Action", result);
    }

    [Fact]
    public void Translate_EnglishMode_UnknownGenre_ReturnsEnglishKey()
    {
        var result = GenreTranslations.Translate("Whatever", Lang.English);
        Assert.Equal("Whatever", result);
    }

    [Fact]
    public void Translate_ThaiMode_Racing_ReturnsThai()
    {
        var result = GenreTranslations.Translate("Racing", Lang.Thai);
        Assert.Equal("แข่งรถ", result);
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
cd /Users/mikeyoshino/gitRepos/KiddoGame
dotnet test webapp.Tests/ --filter "GenreTranslationsTests" -v minimal
```

Expected: build error — `GenreTranslations` not found.

- [ ] **Step 3: Create `GenreTranslations.cs`**

Create `webapp/Services/GenreTranslations.cs`:

```csharp
using Kiddo.Web.Models;

namespace Kiddo.Web.Services;

public static class GenreTranslations
{
    private static readonly Dictionary<string, string> _thaiMap = new()
    {
        ["Action"]         = "แอ็กชัน",
        ["Adventure"]      = "การผจญภัย",
        ["Arcade"]         = "อาร์เคด",
        ["Board"]          = "เกมกระดาน",
        ["Car"]            = "รถยนต์",
        ["Card"]           = "เกมไพ่",
        ["Casino"]         = "คาสิโน",
        ["Casual"]         = "แคชวล",
        ["Clicker"]        = "คลิกเกอร์",
        ["Cooking"]        = "ทำอาหาร",
        ["Educational"]    = "การศึกษา",
        ["Endless Runner"] = "วิ่งไม่หยุด",
        ["Girls"]          = "เกมสำหรับเด็กผู้หญิง",
        ["Horror"]         = "สยองขวัญ",
        ["Hypercasual"]    = "ไฮเปอร์แคชวล",
        ["Idle"]           = "ไอเดิล",
        ["IO"]             = "ไอโอ",
        ["Kids"]           = "เกมเด็ก",
        ["Match3"]         = "จับคู่สาม",
        ["Multiplayer"]    = "หลายผู้เล่น",
        ["Music"]          = "ดนตรี",
        ["Platform"]       = "แพลตฟอร์ม",
        ["Puzzle"]         = "ปริศนา",
        ["Racing"]         = "แข่งรถ",
        ["Role Playing"]   = "เล่นตามบทบาท",
        ["Shooting"]       = "ยิงปืน",
        ["Simulation"]     = "จำลองสถานการณ์",
        ["Soccer"]         = "ฟุตบอล",
        ["Sports"]         = "กีฬา",
        ["Strategy"]       = "กลยุทธ์",
        ["Tower Defense"]  = "ป้องกันฐาน",
        ["Word"]           = "เกมคำศัพท์",
    };

    public static string Translate(string englishKey, Lang lang) =>
        lang == Lang.Thai && _thaiMap.TryGetValue(englishKey, out var thai)
            ? thai : englishKey;
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
dotnet test webapp.Tests/ --filter "GenreTranslationsTests" -v minimal
```

Expected: all 6 tests PASS.

- [ ] **Step 5: Run full test suite to confirm no regressions**

```bash
dotnet test webapp.Tests/ -v minimal
```

Expected: all tests PASS.

- [ ] **Step 6: Commit**

```bash
git add webapp/Services/GenreTranslations.cs webapp.Tests/GenreTranslationsTests.cs
git commit -m "feat: add GenreTranslations static class with Thai genre mapping"
```

---

### Task 2: Update `FilterSidebar.razor` to display translated genres

**Files:**
- Modify: `webapp/Components/Pages/FilterSidebar.razor`

The sidebar needs to:
1. Accept a `Lang` parameter so it knows the active language
2. Display translated genre names instead of raw English
3. Search against the translated text in the active language

- [ ] **Step 1: Add `Lang` parameter and update display label**

Open `webapp/Components/Pages/FilterSidebar.razor`.

In the `@code` block, add the parameter after the existing parameters (around line 96):

```csharp
[Parameter, EditorRequired] public List<(string Genre, int Count)> Genres { get; set; } = [];
[Parameter, EditorRequired] public HashSet<string> SelectedGenres { get; set; } = [];
[Parameter, EditorRequired] public EventCallback<HashSet<string>> OnSelectionChanged { get; set; }
[Parameter] public Lang Lang { get; set; } = Lang.English;
```

- [ ] **Step 2: Update the display label in the genre list**

Find this line (around line 81):
```razor
<span class="text-sm text-slate-600 truncate">@g</span>
```

Replace with:
```razor
<span class="text-sm text-slate-600 truncate">@GenreTranslations.Translate(g, Lang)</span>
```

- [ ] **Step 3: Update `FilteredGenres` to search translated text**

Find `FilteredGenres` (around line 104):
```csharp
private IEnumerable<(string Genre, int Count)> FilteredGenres =>
    string.IsNullOrWhiteSpace(_search)
        ? Genres
        : Genres.Where(g => g.Genre.Contains(_search, StringComparison.OrdinalIgnoreCase));
```

Replace with:
```csharp
private IEnumerable<(string Genre, int Count)> FilteredGenres =>
    string.IsNullOrWhiteSpace(_search)
        ? Genres
        : Genres.Where(g => GenreTranslations.Translate(g.Genre, Lang).Contains(_search, StringComparison.OrdinalIgnoreCase));
```

- [ ] **Step 4: Add the using directive at the top of the file**

The file starts with `@using Kiddo.Web.Models`. Add the services namespace:

```razor
@using Kiddo.Web.Models
@using Kiddo.Web.Services
```

- [ ] **Step 5: Build to verify no compile errors**

```bash
dotnet build webapp/Kiddo.Web.csproj -v minimal
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add webapp/Components/Pages/FilterSidebar.razor
git commit -m "feat: add Lang param to FilterSidebar, display and search translated genre names"
```

---

### Task 3: Update `Home.razor` — pass Lang to sidebar and translate heading

**Files:**
- Modify: `webapp/Components/Pages/Home.razor`

- [ ] **Step 1: Pass current language to FilterSidebar**

Find the FilterSidebar component call (around line 25):
```razor
<FilterSidebar Genres="_genres"
               SelectedGenres="_selectedGenres"
               OnSelectionChanged="OnGenreChanged" />
```

Replace with:
```razor
<FilterSidebar Genres="_genres"
               SelectedGenres="_selectedGenres"
               OnSelectionChanged="OnGenreChanged"
               Lang="LangSvc.Current" />
```

- [ ] **Step 2: Translate selected genre names in the heading**

Find the heading (around line 33):
```razor
@(_selectedGenres.Count == 0
    ? Strings.Get(LangSvc.Current, "all_games")
    : string.Join(", ", _selectedGenres))
```

Replace with:
```razor
@(_selectedGenres.Count == 0
    ? Strings.Get(LangSvc.Current, "all_games")
    : string.Join(", ", _selectedGenres.Select(g => GenreTranslations.Translate(g, LangSvc.Current))))
```

- [ ] **Step 3: Build to verify no compile errors**

```bash
dotnet build webapp/Kiddo.Web.csproj -v minimal
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run full test suite**

```bash
dotnet test webapp.Tests/ -v minimal
```

Expected: all tests PASS.

- [ ] **Step 5: Commit**

```bash
git add webapp/Components/Pages/Home.razor
git commit -m "feat: pass Lang to FilterSidebar and translate selected genre heading in Home"
```

---

### Task 4: Update `GamePage.razor` — translate category badges

**Files:**
- Modify: `webapp/Components/Pages/GamePage.razor`

- [ ] **Step 1: Translate category badge text**

Find the category badge loop (around line 107):
```razor
@foreach (var cat in _game.Categories)
{
    <span class="px-3 py-1 bg-indigo-50 text-indigo-600 rounded-full text-xs font-medium">@cat</span>
}
```

Replace with:
```razor
@foreach (var cat in _game.Categories)
{
    <span class="px-3 py-1 bg-indigo-50 text-indigo-600 rounded-full text-xs font-medium">@GenreTranslations.Translate(cat, LangSvc.Current)</span>
}
```

- [ ] **Step 2: Add the services using directive**

Find the existing `@using` at the top of `GamePage.razor` (line 1 area — the file uses `@inject` but no explicit `@using Kiddo.Web.Services`). Check the `_Imports.razor` to confirm if the namespace is already globally imported.

Open `webapp/Components/_Imports.razor`:

```bash
cat webapp/Components/_Imports.razor
```

If `@using Kiddo.Web.Services` is already there, no action needed. If not, add it to `_Imports.razor` (preferred) or at the top of `GamePage.razor`.

- [ ] **Step 3: Build to verify no compile errors**

```bash
dotnet build webapp/Kiddo.Web.csproj -v minimal
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run full test suite**

```bash
dotnet test webapp.Tests/ -v minimal
```

Expected: all tests PASS.

- [ ] **Step 5: Commit**

```bash
git add webapp/Components/Pages/GamePage.razor
git commit -m "feat: translate category badges on GamePage using GenreTranslations"
```

---

### Task 5: Manual smoke test

- [ ] **Step 1: Start the dev server**

```bash
cd /Users/mikeyoshino/gitRepos/KiddoGame/webapp
dotnet run
```

Open `http://localhost:5000` (or whichever port is shown).

- [ ] **Step 2: Verify FilterSidebar in Thai mode**

1. Switch language to Thai via the language switcher
2. Open the genre accordion in the sidebar
3. Confirm genres display Thai names (e.g. "แอ็กชัน", "ปริศนา")
4. Type "ปริ" in the genre search box — "Puzzle" row should appear
5. Type "action" in the genre search box — no results (search is Thai text when Thai mode)
6. Type "แอ็ก" — "Action" row should appear

- [ ] **Step 3: Verify filter still works correctly**

1. In Thai mode, tick "ปริศนา" (Puzzle)
2. Heading should show "ปริศนา"
3. Games should be filtered to Puzzle games (proving English key "Puzzle" was sent to API)

- [ ] **Step 4: Verify English fallback**

1. Switch to English mode
2. Genre sidebar should show English names
3. Genre search should match English text

- [ ] **Step 5: Verify GamePage category badges**

1. Click any game to open its detail page
2. In Thai mode, category badges should show Thai (e.g. "แคชวล")
3. Switch to English — badges should revert to English

- [ ] **Step 6: Verify unknown genre fallback**

If any game has a category not in the map (e.g. "2 Player"), it should display as-is in both languages.
