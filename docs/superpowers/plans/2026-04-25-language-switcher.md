# Language Switcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Thai/English language switcher (flag dropdown, top-right header) with browser locale auto-detection, localStorage persistence, and full UI string coverage across all pages.

**Architecture:** A scoped `LanguageService` holds the current `Lang` enum and fires `OnChanged` on switch. `MainLayout` wraps `@Body` in `<CascadingValue Value="LangSvc">` so all pages receive it via `[CascadingParameter]`. JS interop in `MainLayout.OnAfterRenderAsync` reads `localStorage["lang"]` or `navigator.language` to set the initial language. A static `Strings` class provides all UI strings keyed by `Lang`.

**Tech Stack:** Blazor Server (.NET 8), Tailwind CSS (CDN), Blazor.Flags 1.0.0.1, Supabase (PostgREST)

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `webapp/Models/Lang.cs` | Create | `Lang` enum (`Thai`, `English`) |
| `webapp/Services/LanguageService.cs` | Create | Language state + `OnChanged` event |
| `webapp/Services/Strings.cs` | Create | All UI strings for both languages |
| `webapp/Models/Game.cs` | Modify | Add `DescriptionTh?`, `InstructionTh?` |
| `webapp/Kiddo.Web.csproj` | Modify | Add `Blazor.Flags` NuGet reference |
| `webapp/Components/_Imports.razor` | Modify | Add `@using Blazor.Flags` |
| `webapp/Components/App.razor` | Modify | Add `getLang`/`setLang` JS helpers |
| `webapp/Program.cs` | Modify | Register `LanguageService` as scoped |
| `webapp/Components/Layout/MainLayout.razor` | Modify | `CascadingValue`, locale detection, switcher UI, `IDisposable` |
| `webapp/Components/Pages/Home.razor` | Modify | `[CascadingParameter]`, replace all hardcoded Thai strings |
| `webapp/Components/Pages/GamePage.razor` | Modify | `[CascadingParameter]`, Thai content fields, replace all strings |

---

## Task 1: Install Blazor.Flags

**Files:**
- Modify: `webapp/Kiddo.Web.csproj`
- Modify: `webapp/Components/_Imports.razor`

- [ ] **Step 1: Add package reference to `Kiddo.Web.csproj`**

Inside the existing `<ItemGroup>` that contains other `<PackageReference>` entries, add:

```xml
<PackageReference Include="Blazor.Flags" Version="1.0.0.1" />
```

- [ ] **Step 2: Restore packages**

```bash
cd /path/to/webapp
dotnet restore
```

Expected: output mentions `Blazor.Flags 1.0.0.1`.

- [ ] **Step 3: Add `@using Blazor.Flags` to `_Imports.razor`**

`webapp/Components/_Imports.razor` already has `@using Kiddo.Web.Models` and `@using Kiddo.Web.Services`. Append one line:

```razor
@using Blazor.Flags
```

- [ ] **Step 4: Commit**

```bash
git add webapp/Kiddo.Web.csproj webapp/Components/_Imports.razor
git commit -m "chore: install Blazor.Flags 1.0.0.1"
```

---

## Task 2: Create `Lang` enum, `LanguageService`, and register in DI

**Files:**
- Create: `webapp/Models/Lang.cs`
- Create: `webapp/Services/LanguageService.cs`
- Modify: `webapp/Program.cs`

- [ ] **Step 1: Create `webapp/Models/Lang.cs`**

```csharp
namespace Kiddo.Web.Models;

public enum Lang { Thai, English }
```

- [ ] **Step 2: Create `webapp/Services/LanguageService.cs`**

```csharp
using Kiddo.Web.Models;

namespace Kiddo.Web.Services;

public class LanguageService
{
    public Lang Current { get; private set; } = Lang.Thai;
    public event Action? OnChanged;

    public void SetLanguage(Lang lang)
    {
        Current = lang;
        OnChanged?.Invoke();
    }
}
```

- [ ] **Step 3: Register service in `webapp/Program.cs`**

Add after the existing `builder.Services.AddHttpClient<GameService>(...)` call:

```csharp
builder.Services.AddScoped<LanguageService>();
```

- [ ] **Step 4: Verify build**

```bash
dotnet build
```

Expected: `Build succeeded. 0 Error(s).`

- [ ] **Step 5: Commit**

```bash
git add webapp/Models/Lang.cs webapp/Services/LanguageService.cs webapp/Program.cs
git commit -m "feat: add Lang enum and LanguageService"
```

---

## Task 3: Create `Strings` helper

**Files:**
- Create: `webapp/Services/Strings.cs`

- [ ] **Step 1: Create `webapp/Services/Strings.cs`**

```csharp
using Kiddo.Web.Models;

namespace Kiddo.Web.Services;

public static class Strings
{
    private static readonly Dictionary<string, (string Thai, string English)> _table = new()
    {
        ["search_placeholder"] = ("ค้นหาเกมที่อยากเล่น...",                  "Search for a game..."),
        ["all_categories"]     = ("🌟 ทั้งหมด",                               "🌟 All"),
        ["all_games"]          = ("เกมทั้งหมด",                               "All Games"),
        ["items_count"]        = ("{0} รายการ",                               "{0} items"),
        ["loading"]            = ("กำลังโหลด...",                             "Loading..."),
        ["no_results"]         = ("ไม่พบเกมที่คุณค้นหา ลองเปลี่ยนคำดูนะ",   "No games found. Try a different search."),
        ["prev_page"]          = ("← ก่อนหน้า",                              "← Prev"),
        ["next_page"]          = ("ถัดไป →",                                  "Next →"),
        ["page_of"]            = ("หน้า {0} / {1}",                           "Page {0} / {1}"),
        ["by_company"]         = ("โดย {0}",                                  "by {0}"),
        ["how_to_play"]        = ("วิธีเล่น",                                 "How to play"),
        ["similar_games"]      = ("เกมที่คล้ายกัน",                           "Similar games"),
        ["game_not_found"]     = ("ไม่พบเกมนี้",                              "Game not found"),
        ["back_home"]          = ("← กลับหน้าแรก",                           "← Back to home"),
        ["tagline"]            = ("เล่นสนุก เรียนรู้ไว 🌈",                   "Play & Learn 🌈"),
    };

    public static string Get(Lang lang, string key, params object[] args)
    {
        if (!_table.TryGetValue(key, out var pair)) return key;
        var template = lang == Lang.Thai ? pair.Thai : pair.English;
        return args.Length > 0 ? string.Format(template, args) : template;
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

Expected: `Build succeeded. 0 Error(s).`

- [ ] **Step 3: Commit**

```bash
git add webapp/Services/Strings.cs
git commit -m "feat: add Strings helper with all Thai/English UI labels"
```

---

## Task 4: Add Thai content fields to `Game` model

**Files:**
- Modify: `webapp/Models/Game.cs`

- [ ] **Step 1: Add two nullable properties after `Instruction`**

The full updated `webapp/Models/Game.cs`:

```csharp
namespace Kiddo.Web.Models;

public class Game
{
    public string Id { get; set; } = "";
    public string ObjectId { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Company { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Description { get; set; }
    public string? Instruction { get; set; }
    public string? DescriptionTh { get; set; }
    public string? InstructionTh { get; set; }
    public string[] Categories { get; set; } = [];
    public string[] Tags { get; set; } = [];
    public string[] Languages { get; set; } = [];
    public string[] Gender { get; set; } = [];
    public string[] AgeGroup { get; set; } = [];
    public string Status { get; set; } = "";
    public int ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public string GameUrl => $"https://html5.gamedistribution.com/{ObjectId}/?gd_sdk_referrer_url=https://gamedistribution.com/games/{Slug}/";
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

Expected: `Build succeeded. 0 Error(s).`

- [ ] **Step 3: Commit**

```bash
git add webapp/Models/Game.cs
git commit -m "feat: add DescriptionTh and InstructionTh to Game model"
```

---

## Task 5: Add JS locale helpers to `App.razor`

**Files:**
- Modify: `webapp/Components/App.razor`

- [ ] **Step 1: Add second `<script>` block before `</body>`**

In `webapp/Components/App.razor`, add this immediately before the closing `</body>` tag (after the existing `<script>` block):

```html
<script>
    window.getLang = function () {
        var stored = localStorage.getItem("lang");
        if (stored) return stored;
        return navigator.language.startsWith("th") ? "th" : "en";
    };
    window.setLang = function (lang) {
        localStorage.setItem("lang", lang);
    };
</script>
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

Expected: `Build succeeded. 0 Error(s).`

- [ ] **Step 3: Commit**

```bash
git add webapp/Components/App.razor
git commit -m "feat: add getLang/setLang JS helpers for locale detection and persistence"
```

---

## Task 6: Rewire `MainLayout.razor` — CascadingValue, locale detection, switcher UI

**Files:**
- Modify: `webapp/Components/Layout/MainLayout.razor`

- [ ] **Step 1: Replace the entire file with the updated version**

```razor
@inherits LayoutComponentBase
@implements IDisposable
@inject IJSRuntime JS
@inject LanguageService LangSvc

<CascadingValue Value="LangSvc">
    <div class="min-h-screen pb-20 md:pb-0">
        <header class="sticky top-0 bg-slate-50/90 backdrop-blur-sm z-40 px-6 py-4 max-w-7xl mx-auto w-full">
            <div class="flex items-center justify-between">
                <a href="/">
                    <h1 class="text-2xl font-bold bg-gradient-to-r from-indigo-500 to-purple-500 bg-clip-text text-transparent">
                        KiddoGame
                    </h1>
                    <p class="text-slate-400 text-sm font-light">@Strings.Get(LangSvc.Current, "tagline")</p>
                </a>

                <!-- Language Switcher -->
                <div class="relative">
                    <button @onclick="ToggleDropdown"
                            class="flex items-center gap-1.5 px-3 py-2 rounded-xl bg-white shadow-sm border border-slate-100 hover:border-indigo-200 transition-colors">
                        @if (LangSvc.Current == Lang.Thai)
                        {
                            <CountryFlag Country="Country.TH" Size="FlagSize.Small" />
                        }
                        else
                        {
                            <CountryFlag Country="Country.GB" Size="FlagSize.Small" />
                        }
                        <svg class="w-3 h-3 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
                        </svg>
                    </button>

                    @if (_dropdownOpen)
                    {
                        <div class="fixed inset-0 z-40" @onclick="CloseDropdown"></div>
                        <div class="absolute right-0 mt-2 w-44 bg-white rounded-xl shadow-lg border border-slate-100 overflow-hidden z-50">
                            <button @onclick='() => SelectLanguage(Lang.Thai)'
                                    class="w-full flex items-center gap-2 px-4 py-2.5 text-sm hover:bg-slate-50 @(LangSvc.Current == Lang.Thai ? "bg-indigo-50 text-indigo-600 font-medium" : "")">
                                <CountryFlag Country="Country.TH" Size="FlagSize.Small" />
                                ภาษาไทย
                            </button>
                            <button @onclick='() => SelectLanguage(Lang.English)'
                                    class="w-full flex items-center gap-2 px-4 py-2.5 text-sm hover:bg-slate-50 @(LangSvc.Current == Lang.English ? "bg-indigo-50 text-indigo-600 font-medium" : "")">
                                <CountryFlag Country="Country.GB" Size="FlagSize.Small" />
                                English
                            </button>
                        </div>
                    }
                </div>
            </div>
        </header>

        <main class="max-w-7xl mx-auto px-6 py-4">
            @Body
        </main>
    </div>
</CascadingValue>

<MobileNav />

@code {
    private bool _dropdownOpen;

    protected override void OnInitialized()
    {
        LangSvc.OnChanged += OnLanguageChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        var code = await JS.InvokeAsync<string>("getLang");
        LangSvc.SetLanguage(code == "th" ? Lang.Thai : Lang.English);
    }

    private void OnLanguageChanged() => InvokeAsync(StateHasChanged);

    private void ToggleDropdown() => _dropdownOpen = !_dropdownOpen;
    private void CloseDropdown() => _dropdownOpen = false;

    private async Task SelectLanguage(Lang lang)
    {
        _dropdownOpen = false;
        LangSvc.SetLanguage(lang);
        await JS.InvokeVoidAsync("setLang", lang == Lang.Thai ? "th" : "en");
    }

    public void Dispose()
    {
        LangSvc.OnChanged -= OnLanguageChanged;
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

Expected: `Build succeeded. 0 Error(s).`

- [ ] **Step 3: Commit**

```bash
git add webapp/Components/Layout/MainLayout.razor
git commit -m "feat: add language switcher UI and CascadingValue to MainLayout"
```

---

## Task 7: Update `Home.razor` with language support

**Files:**
- Modify: `webapp/Components/Pages/Home.razor`

- [ ] **Step 1: Replace the entire file**

```razor
@page "/"
@rendermode InteractiveServer
@inject GameService GameSvc

<!-- Search Bar -->
<div class="relative group mb-6">
    <input type="text"
           placeholder="@Strings.Get(LangSvc.Current, "search_placeholder")"
           class="w-full bg-white border-none rounded-2xl py-4 px-12 shadow-sm focus:ring-2 focus:ring-indigo-300 outline-none"
           @oninput="OnSearchInput" />
    <svg xmlns="http://www.w3.org/2000/svg"
         class="h-6 w-6 absolute left-4 top-1/2 -translate-y-1/2 text-slate-300"
         fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
    </svg>
</div>

<!-- Category Pills -->
<section class="mb-8">
    <div class="flex gap-3 overflow-x-auto hide-scrollbar pb-2">
        <button class="category-pill @(_category == null ? "active" : "") whitespace-nowrap flex items-center gap-2 px-6 py-3 rounded-2xl bg-white shadow-sm border border-slate-100"
                @onclick='() => SelectCategory(null)'>
            <span class="text-sm font-medium">@Strings.Get(LangSvc.Current, "all_categories")</span>
        </button>
        @foreach (var cat in _categories)
        {
            var c = cat;
            <button class="category-pill @(_category == c ? "active" : "") whitespace-nowrap flex items-center gap-2 px-6 py-3 rounded-2xl bg-white shadow-sm border border-slate-100 hover:border-indigo-200"
                    @onclick="() => SelectCategory(c)">
                <span class="text-sm font-medium">@c</span>
            </button>
        }
    </div>
</section>

<!-- Game Grid -->
<section>
    <div class="flex items-center justify-between mb-6">
        <h2 class="text-xl font-bold">
            @(_category is null ? Strings.Get(LangSvc.Current, "all_games") : _category)
        </h2>
        <span class="text-indigo-500 text-sm font-medium">@Strings.Get(LangSvc.Current, "items_count", _total)</span>
    </div>

    @if (_loading)
    {
        <div class="flex justify-center py-20">
            <div class="text-slate-400 text-center">
                <div class="text-5xl mb-4 animate-bounce">🎮</div>
                <p>@Strings.Get(LangSvc.Current, "loading")</p>
            </div>
        </div>
    }
    else if (_games.Count == 0)
    {
        <div class="py-12 text-center text-slate-400">
            <div class="text-5xl mb-4">🔍</div>
            <p>@Strings.Get(LangSvc.Current, "no_results")</p>
        </div>
    }
    else
    {
        <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6 gap-6 mb-8">
            @foreach (var game in _games)
            {
                <a href="/games/@game.Slug" class="game-card group cursor-pointer block">
                    <div class="bg-indigo-50 rounded-[2.5rem] aspect-square flex items-center justify-center relative mb-3 transition-all duration-300 group-hover:-translate-y-2 group-hover:shadow-xl group-hover:shadow-indigo-100 border border-transparent group-hover:border-white overflow-hidden">
                        @if (!string.IsNullOrEmpty(game.ThumbnailUrl))
                        {
                            <img src="@game.ThumbnailUrl" alt="@game.Title"
                                 class="w-full h-full object-cover rounded-3xl" />
                        }
                        else
                        {
                            <span class="text-5xl">🎮</span>
                        }
                    </div>
                    <h3 class="font-semibold text-center text-sm md:text-base group-hover:text-indigo-600 truncate">
                        @game.Title
                    </h3>
                    <p class="text-[11px] text-slate-400 text-center mt-0.5 truncate">@game.Company</p>
                </a>
            }
        </div>

        <!-- Pagination -->
        @if (_totalPages > 1)
        {
            <div class="flex justify-center items-center gap-2 py-6">
                <button class="px-4 py-2 rounded-xl bg-white shadow-sm text-sm disabled:opacity-40"
                        disabled="@(_page == 1)"
                        @onclick="PrevPage">
                    @Strings.Get(LangSvc.Current, "prev_page")
                </button>
                <span class="text-sm text-slate-500">@Strings.Get(LangSvc.Current, "page_of", _page, _totalPages)</span>
                <button class="px-4 py-2 rounded-xl bg-white shadow-sm text-sm disabled:opacity-40"
                        disabled="@(_page == _totalPages)"
                        @onclick="NextPage">
                    @Strings.Get(LangSvc.Current, "next_page")
                </button>
            </div>
        }
    }
</section>

@code {
    [CascadingParameter] public LanguageService LangSvc { get; set; } = default!;

    private List<Game> _games = [];
    private List<string> _categories = [];
    private string? _category;
    private string? _search;
    private int _page = 1;
    private int _total;
    private int _totalPages;
    private bool _loading = true;
    private System.Threading.Timer? _debounce;
    private const int PageSize = 30;

    protected override async Task OnInitializedAsync()
    {
        _categories = await GameSvc.GetCategoriesAsync();
        await LoadGames();
    }

    private async Task LoadGames()
    {
        _loading = true;
        var (games, total) = await GameSvc.GetGamesAsync(_page, PageSize, _category, _search);
        _games = games;
        _total = total;
        _totalPages = (int)Math.Ceiling(total / (double)PageSize);
        _loading = false;
    }

    private async Task SelectCategory(string? cat)
    {
        _category = cat;
        _page = 1;
        await LoadGames();
    }

    private void OnSearchInput(ChangeEventArgs e)
    {
        _debounce?.Dispose();
        _debounce = new System.Threading.Timer(async _ =>
        {
            _search = e.Value?.ToString();
            _page = 1;
            await InvokeAsync(async () =>
            {
                await LoadGames();
                StateHasChanged();
            });
        }, null, 300, System.Threading.Timeout.Infinite);
    }

    private async Task PrevPage()
    {
        if (_page > 1) { _page--; await LoadGames(); }
    }

    private async Task NextPage()
    {
        if (_page < _totalPages) { _page++; await LoadGames(); }
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

Expected: `Build succeeded. 0 Error(s).`

- [ ] **Step 3: Commit**

```bash
git add webapp/Components/Pages/Home.razor
git commit -m "feat: add language support to Home page"
```

---

## Task 8: Update `GamePage.razor` with language support and Thai content fields

**Files:**
- Modify: `webapp/Components/Pages/GamePage.razor`

- [ ] **Step 1: Replace the entire file**

```razor
@page "/games/{Slug}"
@rendermode InteractiveServer
@inject GameService GameSvc
@inject NavigationManager Nav

@if (_loading)
{
    <div class="flex justify-center items-center min-h-[60vh]">
        <div class="text-slate-400 text-center">
            <div class="text-6xl mb-4 animate-bounce">🎮</div>
            <p class="animate-pulse">@Strings.Get(LangSvc.Current, "loading")</p>
        </div>
    </div>
}
else if (_game is null)
{
    <div class="flex justify-center items-center min-h-[60vh]">
        <div class="text-center text-slate-400">
            <div class="text-5xl mb-4">😕</div>
            <p>@Strings.Get(LangSvc.Current, "game_not_found")</p>
            <a href="/" class="mt-4 inline-block text-indigo-500">@Strings.Get(LangSvc.Current, "back_home")</a>
        </div>
    </div>
}
else
{
    var description = LangSvc.Current == Lang.Thai && !string.IsNullOrEmpty(_game.DescriptionTh)
        ? _game.DescriptionTh : _game.Description;
    var instruction = LangSvc.Current == Lang.Thai && !string.IsNullOrEmpty(_game.InstructionTh)
        ? _game.InstructionTh : _game.Instruction;

    <div class="lg:grid lg:grid-cols-[1fr_280px] lg:gap-6">
        <!-- Left: iframe + info -->
        <div>
            <!-- Game iframe -->
            <div class="w-full aspect-video bg-slate-900 rounded-2xl overflow-hidden mb-6 shadow-xl relative group" id="game-wrapper">
                <iframe id="game-iframe"
                        src="@_game.GameUrl"
                        class="w-full h-full"
                        allowfullscreen
                        allow="autoplay; fullscreen *; geolocation; microphone; camera; midi; monetization; xr-spatial-tracking; gamepad; gyroscope; accelerometer; xr; cross-origin-isolated; web-share">
                </iframe>
                <button onclick="enterFullscreen()"
                        class="absolute bottom-3 right-3 z-10
                               bg-black/50 hover:bg-black/80
                               text-white rounded-lg p-2
                               opacity-0 group-hover:opacity-100
                               transition-opacity duration-200
                               backdrop-blur-sm"
                        title="เต็มจอ">
                    <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M4 8V4m0 0h4M4 4l5 5m11-5h-4m4 0v4m0-4l-5 5M4 16v4m0 0h4m-4 0l5-5m11 5l-5-5m5 5v-4m0 4h-4" />
                    </svg>
                </button>
            </div>

            <!-- Game Info -->
            <div class="bg-white rounded-2xl p-6 shadow-sm mb-6">
                <h1 class="text-2xl font-bold mb-1">@_game.Title</h1>
                @if (!string.IsNullOrEmpty(_game.Company))
                {
                    <p class="text-slate-400 text-sm mb-4">@Strings.Get(LangSvc.Current, "by_company", _game.Company)</p>
                }

                @if (_game.Categories.Length > 0)
                {
                    <div class="flex flex-wrap gap-2 mb-4">
                        @foreach (var cat in _game.Categories)
                        {
                            <span class="px-3 py-1 bg-indigo-50 text-indigo-600 rounded-full text-xs font-medium">@cat</span>
                        }
                    </div>
                }

                @if (!string.IsNullOrEmpty(description))
                {
                    <p class="text-slate-600 text-sm leading-relaxed mb-4">@description</p>
                }

                @if (!string.IsNullOrEmpty(instruction))
                {
                    <div class="bg-slate-50 rounded-xl p-4">
                        <h3 class="font-semibold text-sm mb-2">@Strings.Get(LangSvc.Current, "how_to_play")</h3>
                        <p class="text-slate-600 text-sm leading-relaxed whitespace-pre-line">@instruction</p>
                    </div>
                }

                @if (_game.Tags.Length > 0)
                {
                    <div class="flex flex-wrap gap-2 mt-4">
                        @foreach (var tag in _game.Tags)
                        {
                            <span class="px-3 py-1 bg-slate-100 text-slate-500 rounded-full text-xs">#@tag</span>
                        }
                    </div>
                }
            </div>

            <!-- Similar Games (mobile) -->
            @if (_similar.Count > 0)
            {
                <div class="lg:hidden mb-6">
                    <h2 class="font-bold mb-4">@Strings.Get(LangSvc.Current, "similar_games")</h2>
                    <div class="flex gap-3 overflow-x-auto hide-scrollbar pb-2">
                        @foreach (var g in _similar)
                        {
                            <a href="/games/@g.Slug" class="flex-shrink-0 w-24 group">
                                <div class="bg-indigo-50 rounded-2xl aspect-square overflow-hidden transition-transform duration-200 group-hover:scale-105">
                                    @if (!string.IsNullOrEmpty(g.ThumbnailUrl))
                                    {
                                        <img src="@g.ThumbnailUrl" alt="@g.Title" class="w-full h-full object-cover" />
                                    }
                                    else
                                    {
                                        <div class="w-full h-full flex items-center justify-center text-3xl">🎮</div>
                                    }
                                </div>
                            </a>
                        }
                    </div>
                </div>
            }
        </div>

        <!-- Right: Similar Games (desktop sidebar) -->
        @if (_similar.Count > 0)
        {
            <aside class="hidden lg:block">
                <h2 class="font-bold mb-4">@Strings.Get(LangSvc.Current, "similar_games")</h2>
                <div class="flex flex-col gap-2">
                    @foreach (var g in _similar)
                    {
                        <a href="/games/@g.Slug" class="group">
                            <div class="bg-indigo-50 rounded-xl aspect-video overflow-hidden transition-transform duration-200 group-hover:scale-[1.02]">
                                @if (!string.IsNullOrEmpty(g.ThumbnailUrl))
                                {
                                    <img src="@g.ThumbnailUrl" alt="@g.Title" class="w-full h-full object-cover" />
                                }
                                else
                                {
                                    <div class="w-full h-full flex items-center justify-center text-2xl">🎮</div>
                                }
                            </div>
                        </a>
                    }
                </div>
            </aside>
        }
    </div>
}

@code {
    [Parameter] public string Slug { get; set; } = "";
    [CascadingParameter] public LanguageService LangSvc { get; set; } = default!;

    private Game? _game;
    private List<Game> _similar = [];
    private bool _loading = true;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _game = await GameSvc.GetGameBySlugAsync(Slug);
        if (_game is not null)
            _similar = await GameSvc.GetSimilarGamesAsync(_game.Categories, Slug);
        _loading = false;
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

Expected: `Build succeeded. 0 Error(s).`

- [ ] **Step 3: Commit**

```bash
git add webapp/Components/Pages/GamePage.razor
git commit -m "feat: add language support and Thai content fields to GamePage"
```

---

## Task 9: Manual verification

- [ ] **Step 1: Start the dev server**

```bash
cd webapp && dotnet run
```

Open `http://localhost:5001` in the browser.

- [ ] **Step 2: Verify auto-detection (Thai)**

Open DevTools → Application → Local Storage → delete `lang` key if present. Reload.
Browser with Thai locale (`th`, `th-TH`): Thai flag 🇹🇭 appears in header. All UI text is in Thai.

- [ ] **Step 3: Verify auto-detection (English)**

Clear `lang` from localStorage. Set browser language to English. Reload.
GB flag 🇬🇧 appears. UI text in English (search bar says "Search for a game...", categories show "🌟 All", etc.).

- [ ] **Step 4: Verify dropdown**

Click the flag button. Dropdown opens showing 🇹🇭 ภาษาไทย and 🇬🇧 English. Active language is highlighted with indigo background. Clicking outside closes dropdown.

- [ ] **Step 5: Verify switching**

Switch to English. All labels update instantly (no page reload). Navigate to `http://localhost:5001/games/underwater-survival`. Description and instruction show English text. Switch back to Thai — `description_th` and `instruction_th` appear (or falls back to English if null).

- [ ] **Step 6: Verify localStorage persistence**

Select English. Close and reopen the tab. 🇬🇧 flag still shows, text still in English. DevTools → Local Storage → `lang` = `"en"`.
