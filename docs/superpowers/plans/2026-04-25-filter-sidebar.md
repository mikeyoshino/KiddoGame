# Filter Sidebar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the horizontal category-pills filter on the Home page with a left-side Genre filter sidebar that is always visible on desktop and slides in as a drawer on mobile/tablet.

**Architecture:** A new `FilterSidebar.razor` component owns the genre accordion UI and mobile drawer state (open/closed). `Home.razor` owns the selected genres set and reloads games when the selection changes. `GameService` is updated to accept a set of categories (OR logic via PostgREST `ov` operator) and to return genre counts from the RPC.

**Tech Stack:** Blazor Server (.NET 8), Tailwind CSS utility classes, xUnit + FakeHandler for service tests, Supabase PostgREST API.

---

## File Map

| Action | Path | Purpose |
|--------|------|---------|
| Create | `scraper/sql/update_get_distinct_categories.sql` | Update RPC to return `(category, count)` |
| Modify | `webapp/Services/GameService.cs` | `GetCategoriesAsync` → counts; `GetGamesAsync` → multi-category |
| Modify | `webapp.Tests/GameServiceTests.cs` | Add/update tests |
| Create | `webapp/Components/Pages/FilterSidebar.razor` | Sidebar component |
| Modify | `webapp/Components/Pages/Home.razor` | Two-column layout, wire FilterSidebar |

---

## Task 1: SQL — extend `get_distinct_categories` to return counts

**Files:**
- Create: `scraper/sql/update_get_distinct_categories.sql`

- [ ] **Step 1.1: Write the SQL migration file**

```sql
-- scraper/sql/update_get_distinct_categories.sql
-- Replaces the existing get_distinct_categories function to also return
-- the count of games per category, sorted by count descending.

CREATE OR REPLACE FUNCTION get_distinct_categories()
RETURNS TABLE(category text, count bigint)
LANGUAGE sql
AS $$
  SELECT unnest(categories) AS category, COUNT(*) AS count
  FROM games
  GROUP BY 1
  ORDER BY 2 DESC;
$$;
```

- [ ] **Step 1.2: Run in Supabase SQL editor**

Open your Supabase project → SQL Editor → paste the file contents → Run.

- [ ] **Step 1.3: Verify the output**

Run this check query in the SQL Editor:

```sql
SELECT * FROM get_distinct_categories() LIMIT 5;
```

Expected: rows with both `category` (text) and `count` (integer) columns.

---

## Task 2: Update `GameService.GetCategoriesAsync` to return genre counts

**Files:**
- Modify: `webapp/Services/GameService.cs`
- Modify: `webapp.Tests/GameServiceTests.cs`

- [ ] **Step 2.1: Write the failing test**

Replace the existing `GetCategoriesAsync_ReturnsCategories` test in `webapp.Tests/GameServiceTests.cs`:

```csharp
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
```

- [ ] **Step 2.2: Run to confirm it fails**

```bash
cd /Users/mikeyoshino/gitRepos/KiddoGame
dotnet test webapp.Tests/ --filter "GetCategoriesAsync_ReturnsGenresWithCounts" -v
```

Expected output: **FAIL** — compile error because `GetCategoriesAsync` still returns `List<string>`.

- [ ] **Step 2.3: Update `GameService.cs`**

In `webapp/Services/GameService.cs`, replace the `GetCategoriesAsync` method and `CategoryRow` record:

```csharp
public async Task<List<(string Category, int Count)>> GetCategoriesAsync()
{
    try
    {
        var json = await http.GetStringAsync("rpc/get_distinct_categories");
        var rows = JsonSerializer.Deserialize<List<CategoryRow>>(json, _json) ?? [];
        return rows.Select(r => (r.Category, r.Count)).ToList();
    }
    catch (HttpRequestException)
    {
        return [];
    }
}

private record CategoryRow(string Category, int Count);
```

- [ ] **Step 2.4: Run the test**

```bash
dotnet test webapp.Tests/ --filter "GetCategoriesAsync_ReturnsGenresWithCounts" -v
```

Expected output: **PASS**

- [ ] **Step 2.5: Run the full test suite to catch regressions**

```bash
dotnet test webapp.Tests/ -v
```

Expected output: all tests **PASS**. (The old `GetCategoriesAsync_ReturnsCategories` test was replaced in Step 2.1.)

- [ ] **Step 2.6: Commit**

```bash
cd /Users/mikeyoshino/gitRepos/KiddoGame
git add scraper/sql/update_get_distinct_categories.sql webapp/Services/GameService.cs webapp.Tests/GameServiceTests.cs
git commit -m "feat: extend get_distinct_categories RPC and service to return category counts"
```

---

## Task 3: Update `GameService.GetGamesAsync` for multi-category filter

**Files:**
- Modify: `webapp/Services/GameService.cs`
- Modify: `webapp.Tests/GameServiceTests.cs`

- [ ] **Step 3.1: Write failing tests for `BuildGamesUrl`**

Add these tests to `webapp.Tests/GameServiceTests.cs` (inside `GameServiceTests` class):

```csharp
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
public void BuildGamesUrl_WithSearch_AppendsIlikeFilter()
{
    var url = GameService.BuildGamesUrl(1, 30, null, "mario");
    Assert.Contains("&title=ilike.*mario*", url);
}
```

- [ ] **Step 3.2: Run to confirm failures**

```bash
cd /Users/mikeyoshino/gitRepos/KiddoGame
dotnet test webapp.Tests/ --filter "BuildGamesUrl" -v
```

Expected: **FAIL** — `BuildGamesUrl` does not exist yet.

- [ ] **Step 3.3: Extract `BuildGamesUrl` and update `GetGamesAsync`**

In `webapp/Services/GameService.cs`, replace `GetGamesAsync` with:

```csharp
public async Task<(List<Game> Games, int Total)> GetGamesAsync(
    int page, int pageSize = 30, IReadOnlySet<string>? categories = null, string? search = null)
{
    var url = BuildGamesUrl(page, pageSize, categories, search);

    var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.TryAddWithoutValidation("Prefer", "count=exact");

    var response = await http.SendAsync(request);
    response.EnsureSuccessStatusCode();

    var contentRange = response.Content.Headers.TryGetValues("Content-Range", out var values)
        ? values.FirstOrDefault() : null;
    var total = ParseTotal(contentRange);

    var json = await response.Content.ReadAsStringAsync();
    var games = JsonSerializer.Deserialize<List<Game>>(json, _json) ?? [];
    return (games, total);
}

internal static string BuildGamesUrl(
    int page, int pageSize, IReadOnlySet<string>? categories, string? search)
{
    var offset = (page - 1) * pageSize;
    var url = $"games?select=*&order=created_at.desc&offset={offset}&limit={pageSize}";

    if (categories is { Count: > 0 })
    {
        var cats = string.Join(",", categories);
        url += $"&categories=ov.%7B{Uri.EscapeDataString(cats)}%7D";
    }

    if (!string.IsNullOrEmpty(search))
        url += $"&title=ilike.*{Uri.EscapeDataString(search)}*";

    return url;
}
```

- [ ] **Step 3.4: Run the new tests**

```bash
dotnet test webapp.Tests/ --filter "BuildGamesUrl" -v
```

Expected: all **PASS**

- [ ] **Step 3.5: Run the full test suite**

```bash
dotnet test webapp.Tests/ -v
```

Expected: all **PASS** (the existing `GetGamesAsync_ReturnsGamesAndTotal` still works because `categories` defaults to `null`).

- [ ] **Step 3.6: Commit**

```bash
cd /Users/mikeyoshino/gitRepos/KiddoGame
git add webapp/Services/GameService.cs webapp.Tests/GameServiceTests.cs
git commit -m "feat: support multi-category OR filter in GetGamesAsync via PostgREST ov operator"
```

---

## Task 4: Create `FilterSidebar.razor`

**Files:**
- Create: `webapp/Components/Pages/FilterSidebar.razor`

- [ ] **Step 4.1: Create the component**

Create `webapp/Components/Pages/FilterSidebar.razor` with the full content:

```razor
<!-- Mobile overlay: covers game area when drawer is open, tap to close -->
@if (_drawerOpen)
{
    <div class="fixed inset-0 bg-black/25 z-20 lg:hidden"
         @onclick="CloseDrawer"></div>
}

<!-- Arrow tab: pinned to left edge on mobile when drawer is closed -->
<button class="fixed left-0 top-1/2 -translate-y-1/2 w-6 h-12 bg-indigo-500
               rounded-r-xl flex items-center justify-center text-white z-40
               shadow-lg lg:hidden @(_drawerOpen ? "hidden" : "")"
        @onclick="OpenDrawer"
        aria-label="Open filters">
    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M9 5l7 7-7 7"/>
    </svg>
</button>

<!-- Sidebar panel: fixed + slide on mobile, static on desktop -->
<aside class="fixed top-0 left-0 h-full w-64 bg-white shadow-xl z-30
              transition-transform duration-300 ease-in-out overflow-y-auto
              lg:static lg:h-auto lg:shadow-none lg:z-auto lg:w-64 lg:translate-x-0
              @(_drawerOpen ? "translate-x-0" : "-translate-x-full")">

    <!-- Mobile close button -->
    <div class="flex justify-end p-3 lg:hidden">
        <button @onclick="CloseDrawer"
                class="w-8 h-8 rounded-full bg-indigo-100 text-indigo-600
                       flex items-center justify-center text-sm font-bold hover:bg-indigo-200"
                aria-label="Close filters">
            ✕
        </button>
    </div>

    <div class="px-4 pb-6 pt-2 lg:pt-0">

        <p class="text-xs font-bold text-slate-400 uppercase tracking-widest mb-4">Filters</p>

        <!-- Genre accordion header -->
        <button class="w-full flex items-center justify-between bg-indigo-50 hover:bg-indigo-100
                       rounded-2xl px-4 py-3 font-bold text-indigo-700 mb-2 transition-colors"
                @onclick="() => _genreOpen = !_genreOpen">
            <span>Genre</span>
            <svg class="w-4 h-4 transition-transform duration-200 @(_genreOpen ? "" : "rotate-180")"
                 fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M5 15l7-7 7 7"/>
            </svg>
        </button>

        <!-- Genre panel -->
        @if (_genreOpen)
        {
            <div class="bg-white border border-slate-100 rounded-2xl p-3 shadow-sm">
                <!-- Search input -->
                <div class="flex items-center gap-2 border-b-2 border-indigo-400 pb-2 mb-3">
                    <input type="text"
                           placeholder="Search"
                           class="flex-1 outline-none text-sm text-indigo-700 bg-transparent placeholder:text-indigo-200"
                           @oninput="@(e => _search = e.Value?.ToString() ?? "")" />
                    <svg class="w-4 h-4 text-indigo-500 flex-shrink-0" fill="none"
                         stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                              d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
                    </svg>
                </div>

                <!-- Genre list -->
                <div class="max-h-72 overflow-y-auto flex flex-col gap-0.5 hide-scrollbar">
                    @foreach (var (genre, count) in FilteredGenres)
                    {
                        var g = genre;
                        <label class="flex items-center justify-between py-1.5 px-1
                                      cursor-pointer hover:bg-slate-50 rounded-lg">
                            <div class="flex items-center gap-2 min-w-0">
                                <input type="checkbox"
                                       checked="@SelectedGenres.Contains(g)"
                                       @onchange="@(e => ToggleGenre(g))"
                                       class="w-4 h-4 rounded accent-indigo-500 flex-shrink-0" />
                                <span class="text-sm text-slate-600 truncate">@g</span>
                            </div>
                            <span class="text-xs text-slate-400 bg-slate-100 rounded
                                         px-1.5 py-0.5 flex-shrink-0 ml-2">
                                @count
                            </span>
                        </label>
                    }
                </div>
            </div>
        }
    </div>
</aside>

@code {
    [Parameter, EditorRequired] public List<(string Genre, int Count)> Genres { get; set; } = [];
    [Parameter, EditorRequired] public HashSet<string> SelectedGenres { get; set; } = [];
    [Parameter, EditorRequired] public EventCallback<HashSet<string>> OnSelectionChanged { get; set; }

    private bool _genreOpen = true;
    private bool _drawerOpen = false;
    private string _search = "";

    private IEnumerable<(string Genre, int Count)> FilteredGenres =>
        string.IsNullOrWhiteSpace(_search)
            ? Genres
            : Genres.Where(g => g.Genre.Contains(_search, StringComparison.OrdinalIgnoreCase));

    private async Task ToggleGenre(string genre)
    {
        if (!SelectedGenres.Remove(genre))
            SelectedGenres.Add(genre);
        await OnSelectionChanged.InvokeAsync(new HashSet<string>(SelectedGenres));
    }

    private void OpenDrawer() => _drawerOpen = true;
    private void CloseDrawer() => _drawerOpen = false;
}
```

- [ ] **Step 4.2: Build to check for compile errors**

```bash
cd /Users/mikeyoshino/gitRepos/KiddoGame
dotnet build webapp/Kiddo.Web.csproj
```

Expected: **Build succeeded** with 0 errors.

- [ ] **Step 4.3: Commit**

```bash
git add webapp/Components/Pages/FilterSidebar.razor
git commit -m "feat: add FilterSidebar component with genre accordion and mobile drawer"
```

---

## Task 5: Update `Home.razor` — two-column layout, wire FilterSidebar

**Files:**
- Modify: `webapp/Components/Pages/Home.razor`

- [ ] **Step 5.1: Replace `Home.razor` content**

Replace the entire content of `webapp/Components/Pages/Home.razor`:

```razor
@page "/"
@rendermode InteractiveServer
@inject GameService GameSvc
@inject LanguageService LangSvc
@implements IDisposable

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

<!-- Main layout: sidebar + game area -->
<div class="flex gap-6 items-start">

    <!-- Filter sidebar -->
    <FilterSidebar Genres="_genres"
                   SelectedGenres="_selectedGenres"
                   OnSelectionChanged="OnGenreChanged" />

    <!-- Game area -->
    <div class="flex-1 min-w-0">
        <div class="flex items-center justify-between mb-6">
            <h2 class="text-xl font-bold">
                @(_selectedGenres.Count == 0
                    ? Strings.Get(LangSvc.Current, "all_games")
                    : string.Join(", ", _selectedGenres))
            </h2>
            <span class="text-indigo-500 text-sm font-medium">
                @Strings.Get(LangSvc.Current, "items_count", _total)
            </span>
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
            <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-3 xl:grid-cols-4 gap-6 mb-8">
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
                    <span class="text-sm text-slate-500">
                        @Strings.Get(LangSvc.Current, "page_of", _page, _totalPages)
                    </span>
                    <button class="px-4 py-2 rounded-xl bg-white shadow-sm text-sm disabled:opacity-40"
                            disabled="@(_page == _totalPages)"
                            @onclick="NextPage">
                        @Strings.Get(LangSvc.Current, "next_page")
                    </button>
                </div>
            }
        }
    </div>
</div>

@code {
    private List<Game> _games = [];
    private List<(string Genre, int Count)> _genres = [];
    private HashSet<string> _selectedGenres = [];
    private string? _search;
    private int _page = 1;
    private int _total;
    private int _totalPages;
    private bool _loading = true;
    private System.Threading.Timer? _debounce;
    private const int PageSize = 30;

    protected override void OnInitialized()
    {
        LangSvc.OnChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        LangSvc.OnChanged -= OnLanguageChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        _genres = await GameSvc.GetCategoriesAsync();
        await LoadGames();
    }

    private async Task LoadGames()
    {
        _loading = true;
        var (games, total) = await GameSvc.GetGamesAsync(_page, PageSize, _selectedGenres, _search);
        _games = games;
        _total = total;
        _totalPages = (int)Math.Ceiling(total / (double)PageSize);
        _loading = false;
    }

    private async Task OnGenreChanged(HashSet<string> selected)
    {
        _selectedGenres = selected;
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

- [ ] **Step 5.2: Build**

```bash
cd /Users/mikeyoshino/gitRepos/KiddoGame
dotnet build webapp/Kiddo.Web.csproj
```

Expected: **Build succeeded** with 0 errors.

- [ ] **Step 5.3: Run the full test suite**

```bash
dotnet test webapp.Tests/ -v
```

Expected: all **PASS**

- [ ] **Step 5.4: Start the dev server and verify manually**

```bash
cd /Users/mikeyoshino/gitRepos/KiddoGame/webapp
dotnet run
```

Open the browser (usually `https://localhost:5001` or `http://localhost:5000`).

Verify on desktop (wide window):
- [ ] Sidebar is visible on the left with a "Genre" header
- [ ] Clicking "Genre" collapses/expands the section
- [ ] Typing in the search box filters the genre list
- [ ] Clicking a genre checkbox updates the game grid immediately
- [ ] Multiple genres can be selected (OR logic — any matching game shows)
- [ ] Deselecting all checkboxes shows all games again
- [ ] Pagination still works

Verify on mobile (narrow the browser window below 1024 px):
- [ ] Sidebar is hidden; an indigo arrow tab is visible at the left edge
- [ ] Clicking the arrow tab slides the sidebar in from the left
- [ ] A dim overlay appears behind the sidebar
- [ ] Clicking the overlay closes the sidebar
- [ ] The ✕ button inside the sidebar also closes it
- [ ] Selecting a genre while the drawer is open filters the games (visible after closing drawer)

- [ ] **Step 5.5: Commit**

```bash
cd /Users/mikeyoshino/gitRepos/KiddoGame
git add webapp/Components/Pages/Home.razor
git commit -m "feat: replace category pills with left-side filter sidebar on Home page"
```
