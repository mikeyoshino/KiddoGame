# Favorites Feature Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let users save favourite games to localStorage without logging in, accessible via a heart icon on every game card/page and a Favorites nav link.

**Architecture:** A scoped `FavoritesService` mirrors the existing `LanguageService` pattern — it holds an in-memory `HashSet<string>` of slugs, persists to `localStorage` via `IJSRuntime`, and fires `OnChanged` so any subscribed component re-renders. A new `GetGamesBySlugsAsync` method on `GameService` fetches the saved games in one request using PostgREST's `in.()` operator.

**Tech Stack:** Blazor Server (.NET 8), `IJSRuntime` for localStorage, xUnit for tests, Heroicon SVGs, Tailwind CSS.

---

## File Map

| Action | Path |
|---|---|
| Create | `webapp/Services/FavoritesService.cs` |
| Create | `webapp/Components/Pages/Favorites.razor` |
| Create | `webapp.Tests/FavoritesServiceTests.cs` |
| Modify | `webapp/Services/GameService.cs` — add `GetGamesBySlugsAsync` |
| Modify | `webapp/Services/Strings.cs` — add `favorites`, `no_favorites` |
| Modify | `webapp/Program.cs` — register `FavoritesService` as scoped |
| Modify | `webapp/Components/Pages/Home.razor` — inject service, add heart button |
| Modify | `webapp/Components/Pages/GamePage.razor` — inject service, add heart button |
| Modify | `webapp/Components/Layout/TopNav.razor` — add Favorites link |
| Modify | `webapp/Components/Layout/MobileNav.razor` — add Favorites icon, Heroicons v2 |
| Modify | `webapp.Tests/GameServiceTests.cs` — add `GetGamesBySlugsAsync` tests |

---

## Task 1: FavoritesService — tests + implementation

**Files:**
- Create: `webapp.Tests/FavoritesServiceTests.cs`
- Create: `webapp/Services/FavoritesService.cs`
- Modify: `webapp/Program.cs:9`

- [ ] **Step 1: Write failing tests**

Create `webapp.Tests/FavoritesServiceTests.cs`:

```csharp
using Kiddo.Web.Services;
using Microsoft.JSInterop;

namespace Kiddo.Web.Tests;

public class FavoritesServiceTests
{
    [Fact]
    public async Task LoadAsync_ParsesSlugsFromStorage()
    {
        var js = new FakeJSRuntime();
        js.Store["kiddo_favorites"] = """["slug-a","slug-b"]""";
        var svc = new FavoritesService(js);

        await svc.LoadAsync();

        Assert.True(svc.IsFavorite("slug-a"));
        Assert.True(svc.IsFavorite("slug-b"));
        Assert.False(svc.IsFavorite("slug-c"));
    }

    [Fact]
    public async Task LoadAsync_HandlesEmptyStorage()
    {
        var js = new FakeJSRuntime();
        var svc = new FavoritesService(js);

        await svc.LoadAsync();

        Assert.False(svc.IsFavorite("any-slug"));
    }

    [Fact]
    public async Task LoadAsync_IsNoOpOnSecondCall()
    {
        var js = new FakeJSRuntime();
        var svc = new FavoritesService(js);

        await svc.LoadAsync();
        js.Store["kiddo_favorites"] = """["slug-a"]""";
        await svc.LoadAsync(); // should not re-read

        Assert.False(svc.IsFavorite("slug-a")); // first load was empty
    }

    [Fact]
    public async Task ToggleAsync_AddsFavorite()
    {
        var js = new FakeJSRuntime();
        var svc = new FavoritesService(js);
        await svc.LoadAsync();

        await svc.ToggleAsync("slug-a");

        Assert.True(svc.IsFavorite("slug-a"));
        Assert.Contains("slug-a", js.Store["kiddo_favorites"]);
    }

    [Fact]
    public async Task ToggleAsync_RemovesExistingFavorite()
    {
        var js = new FakeJSRuntime();
        js.Store["kiddo_favorites"] = """["slug-a"]""";
        var svc = new FavoritesService(js);
        await svc.LoadAsync();

        await svc.ToggleAsync("slug-a");

        Assert.False(svc.IsFavorite("slug-a"));
    }

    [Fact]
    public async Task ToggleAsync_FiresOnChanged()
    {
        var js = new FakeJSRuntime();
        var svc = new FavoritesService(js);
        await svc.LoadAsync();
        var fired = false;
        svc.OnChanged += () => fired = true;

        await svc.ToggleAsync("slug-a");

        Assert.True(fired);
    }

    [Fact]
    public async Task GetSlugs_ReturnsCurrentSlugs()
    {
        var js = new FakeJSRuntime();
        js.Store["kiddo_favorites"] = """["slug-a","slug-b"]""";
        var svc = new FavoritesService(js);
        await svc.LoadAsync();

        var slugs = svc.GetSlugs();

        Assert.Contains("slug-a", slugs);
        Assert.Contains("slug-b", slugs);
    }
}

internal class FakeJSRuntime : IJSRuntime
{
    public Dictionary<string, string> Store { get; } = new();

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (identifier == "localStorage.getItem" && args is [var k, ..])
        {
            Store.TryGetValue(k?.ToString() ?? "", out var val);
            return ValueTask.FromResult((TValue)(object?)val!);
        }
        if (identifier == "localStorage.setItem" && args is [var key, var value, ..])
        {
            Store[key?.ToString() ?? ""] = value?.ToString() ?? "";
            return ValueTask.FromResult(default(TValue)!);
        }
        return ValueTask.FromResult(default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
cd webapp.Tests && dotnet test --filter "FavoritesServiceTests" -v minimal
```

Expected: compile error — `FavoritesService` not found.

- [ ] **Step 3: Implement FavoritesService**

Create `webapp/Services/FavoritesService.cs`:

```csharp
using System.Text.Json;
using Microsoft.JSInterop;

namespace Kiddo.Web.Services;

public class FavoritesService(IJSRuntime js)
{
    private const string Key = "kiddo_favorites";
    private HashSet<string> _slugs = [];
    private bool _loaded;

    public event Action? OnChanged;

    public async Task LoadAsync()
    {
        if (_loaded) return;
        var json = await js.InvokeAsync<string?>("localStorage.getItem", Key);
        if (!string.IsNullOrEmpty(json))
        {
            var slugs = JsonSerializer.Deserialize<string[]>(json) ?? [];
            _slugs = new HashSet<string>(slugs);
        }
        _loaded = true;
    }

    public async Task ToggleAsync(string slug)
    {
        if (_slugs.Contains(slug))
            _slugs.Remove(slug);
        else
            _slugs.Add(slug);

        await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(_slugs));
        OnChanged?.Invoke();
    }

    public bool IsFavorite(string slug) => _slugs.Contains(slug);

    public string[] GetSlugs() => [.. _slugs];
}
```

- [ ] **Step 4: Register FavoritesService in Program.cs**

In `webapp/Program.cs`, add this line after the `AddScoped<LanguageService>()` line (line 9):

```csharp
builder.Services.AddScoped<FavoritesService>();
```

- [ ] **Step 5: Run tests to verify they pass**

```
cd webapp.Tests && dotnet test --filter "FavoritesServiceTests" -v minimal
```

Expected: 7 tests pass, 0 failures.

- [ ] **Step 6: Commit**

```bash
git add webapp/Services/FavoritesService.cs webapp/Program.cs webapp.Tests/FavoritesServiceTests.cs
git commit -m "feat: add FavoritesService with localStorage persistence"
```

---

## Task 2: GameService.GetGamesBySlugsAsync — tests + implementation

**Files:**
- Modify: `webapp.Tests/GameServiceTests.cs`
- Modify: `webapp/Services/GameService.cs`

- [ ] **Step 1: Write failing tests**

Add to the bottom of `webapp.Tests/GameServiceTests.cs` (before the closing `}`):

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

```
cd webapp.Tests && dotnet test --filter "GetGamesBySlugsAsync" -v minimal
```

Expected: compile error — `GetGamesBySlugsAsync` not found.

- [ ] **Step 3: Implement GetGamesBySlugsAsync**

Add to `webapp/Services/GameService.cs` after `GetSimilarGamesAsync` (before `GetCategoriesAsync`):

```csharp
public async Task<List<Game>> GetGamesBySlugsAsync(string[] slugs)
{
    if (slugs.Length == 0) return [];
    var inList = string.Join(",", slugs.Select(Uri.EscapeDataString));
    var url = $"games?select=*&slug=in.({inList})";
    var json = await http.GetStringAsync(url);
    return JsonSerializer.Deserialize<List<Game>>(json, _json) ?? [];
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
cd webapp.Tests && dotnet test --filter "GetGamesBySlugsAsync" -v minimal
```

Expected: 2 tests pass, 0 failures.

- [ ] **Step 5: Run full test suite**

```
cd webapp.Tests && dotnet test -v minimal
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add webapp/Services/GameService.cs webapp.Tests/GameServiceTests.cs
git commit -m "feat: add GetGamesBySlugsAsync to GameService"
```

---

## Task 3: Strings — add favorites keys

**Files:**
- Modify: `webapp/Services/Strings.cs`

- [ ] **Step 1: Add the two new keys**

In `webapp/Services/Strings.cs`, add these two entries inside `_table` after the `["goto_page"]` entry:

```csharp
["favorites"]    = ("รายการโปรด",                              "Favorites"),
["no_favorites"] = ("ยังไม่มีเกมโปรด เพิ่มได้เลย!",          "No favorites yet. Start adding some!"),
```

- [ ] **Step 2: Build to verify no compile errors**

```
cd webapp && dotnet build -v minimal
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add webapp/Services/Strings.cs
git commit -m "feat: add favorites string keys (Thai + English)"
```

---

## Task 4: TopNav + MobileNav — add Favorites links

**Files:**
- Modify: `webapp/Components/Layout/TopNav.razor`
- Modify: `webapp/Components/Layout/MobileNav.razor`

- [ ] **Step 1: Update TopNav.razor**

Replace the full content of `webapp/Components/Layout/TopNav.razor` with:

```razor
@rendermode InteractiveServer
@inject LanguageService LangSvc
@inject NavigationManager Nav
@implements IDisposable

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
</nav>

@code {
    private bool IsHome =>
        Nav.Uri == Nav.BaseUri || Nav.Uri.TrimEnd('/') == Nav.BaseUri.TrimEnd('/');

    private bool IsFavorites =>
        Nav.Uri.TrimEnd('/').Equals(Nav.BaseUri.TrimEnd('/') + "favorites", StringComparison.OrdinalIgnoreCase);

    protected override void OnInitialized()
    {
        LangSvc.OnChanged += OnLanguageChanged;
        Nav.LocationChanged += OnLocationChanged;
    }

    private void OnLanguageChanged() => InvokeAsync(StateHasChanged);
    private void OnLocationChanged(object? s, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
        => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        LangSvc.OnChanged -= OnLanguageChanged;
        Nav.LocationChanged -= OnLocationChanged;
    }
}
```

- [ ] **Step 2: Update MobileNav.razor**

Replace the full content of `webapp/Components/Layout/MobileNav.razor` with:

```razor
@rendermode InteractiveServer
@inject NavigationManager Nav
@inject LanguageService LangSvc
@implements IDisposable

<nav class="fixed bottom-0 left-0 right-0 bg-white/80 backdrop-blur-md border-t border-slate-100 flex justify-around items-center py-3 z-50 md:hidden">
    <a href="/" class="flex flex-col items-center @(IsHome ? "text-indigo-600" : "text-slate-400")">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="m2.25 12 8.954-8.955c.44-.439 1.152-.439 1.591 0L21.75 12M4.5 9.75v10.125c0 .621.504 1.125 1.125 1.125H9.75v-4.875c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125V21h4.125c.621 0 1.125-.504 1.125-1.125V9.75M8.25 21h8.25" />
        </svg>
        <span class="text-xs mt-1 font-light">@Strings.Get(LangSvc.Current, "nav_home")</span>
    </a>
    <a href="/favorites" class="flex flex-col items-center @(IsFavorites ? "text-indigo-600" : "text-slate-400")">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="M21 8.25c0-2.485-2.099-4.5-4.688-4.5-1.935 0-3.597 1.126-4.312 2.733-.715-1.607-2.377-2.733-4.313-2.733C5.1 3.75 3 5.765 3 8.25c0 7.22 9 12 9 12s9-4.78 9-12Z" />
        </svg>
        <span class="text-xs mt-1 font-light">@Strings.Get(LangSvc.Current, "favorites")</span>
    </a>
    <a href="/" class="flex flex-col items-center text-slate-400">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
        </svg>
        <span class="text-xs mt-1 font-light">ค้นหา</span>
    </a>
</nav>

@code {
    private bool IsHome =>
        Nav.Uri == Nav.BaseUri || Nav.Uri.TrimEnd('/') == Nav.BaseUri.TrimEnd('/');

    private bool IsFavorites =>
        Nav.Uri.TrimEnd('/').Equals(Nav.BaseUri.TrimEnd('/') + "favorites", StringComparison.OrdinalIgnoreCase);

    protected override void OnInitialized()
    {
        LangSvc.OnChanged += OnLanguageChanged;
        Nav.LocationChanged += OnLocationChanged;
    }

    private void OnLanguageChanged() => InvokeAsync(StateHasChanged);
    private void OnLocationChanged(object? s, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
        => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        LangSvc.OnChanged -= OnLanguageChanged;
        Nav.LocationChanged -= OnLocationChanged;
    }
}
```

- [ ] **Step 3: Build to verify no compile errors**

```
cd webapp && dotnet build -v minimal
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add webapp/Components/Layout/TopNav.razor webapp/Components/Layout/MobileNav.razor
git commit -m "feat: add Favorites nav link to TopNav and MobileNav"
```

---

## Task 5: Home.razor — heart button on game cards

**Files:**
- Modify: `webapp/Components/Pages/Home.razor`

- [ ] **Step 1: Add FavoritesService injection and lifecycle**

In `webapp/Components/Pages/Home.razor`:

1. Add `@inject FavoritesService FavSvc` after line 4 (`@inject LanguageService LangSvc`).

2. In the `@code` block, add to `OnInitialized()`:
```csharp
FavSvc.OnChanged += OnFavoritesChanged;
```

3. Add `OnFavoritesChanged` handler alongside `OnLanguageChanged`:
```csharp
private void OnFavoritesChanged() => InvokeAsync(StateHasChanged);
```

4. Add to `Dispose()`:
```csharp
FavSvc.OnChanged -= OnFavoritesChanged;
```

5. Add `OnAfterRenderAsync` override (after `OnInitializedAsync`):
```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (!firstRender) return;
    await FavSvc.LoadAsync();
    StateHasChanged();
}
```

- [ ] **Step 2: Add heart button to game card**

In `Home.razor`, locate the game card thumbnail `<div>` (the one with class `bg-indigo-50 rounded-[2.5rem]`). It currently contains an `<img>` or emoji fallback. Add the heart button as the last child inside that div, just before its closing `</div>`:

```razor
<button class="absolute top-2 right-2 bg-white rounded-full w-7 h-7 flex items-center justify-center shadow-sm p-1.5
               @(FavSvc.IsFavorite(game.Slug) ? "text-rose-500" : "text-slate-300")"
        @onclick="() => _ = FavSvc.ToggleAsync(game.Slug)"
        @onclick:stopPropagation
        title="@(FavSvc.IsFavorite(game.Slug) ? "Remove favourite" : "Add to favourites")">
    @if (FavSvc.IsFavorite(game.Slug))
    {
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="w-full h-full">
            <path d="m11.645 20.91-.007-.003-.022-.012a15.247 15.247 0 0 1-.383-.218 25.18 25.18 0 0 1-4.244-3.17C4.688 15.36 2.25 12.174 2.25 8.25 2.25 5.322 4.714 3 7.688 3A5.5 5.5 0 0 1 12 5.052 5.5 5.5 0 0 1 16.313 3c2.973 0 5.437 2.322 5.437 5.25 0 3.925-2.438 7.111-4.739 9.256a25.175 25.175 0 0 1-4.244 3.17 15.247 15.247 0 0 1-.383.218l-.022.012-.007.003-.003.001a.752.752 0 0 1-.704 0l-.003-.001Z" />
        </svg>
    }
    else
    {
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-full h-full">
            <path stroke-linecap="round" stroke-linejoin="round" d="M21 8.25c0-2.485-2.099-4.5-4.688-4.5-1.935 0-3.597 1.126-4.312 2.733-.715-1.607-2.377-2.733-4.313-2.733C5.1 3.75 3 5.765 3 8.25c0 7.22 9 12 9 12s9-4.78 9-12Z" />
        </svg>
    }
</button>
```

- [ ] **Step 3: Build to verify no compile errors**

```
cd webapp && dotnet build -v minimal
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add webapp/Components/Pages/Home.razor
git commit -m "feat: add heart toggle button to game cards on Home page"
```

---

## Task 6: GamePage.razor — heart button

**Files:**
- Modify: `webapp/Components/Pages/GamePage.razor`

- [ ] **Step 1: Add FavoritesService injection and lifecycle**

In `webapp/Components/Pages/GamePage.razor`:

1. Add `@inject FavoritesService FavSvc` after line 5 (`@inject LanguageService LangSvc`).

2. In `OnInitialized()`, add:
```csharp
FavSvc.OnChanged += OnFavoritesChanged;
```

3. Add handler alongside `OnLanguageChanged`:
```csharp
private void OnFavoritesChanged() => InvokeAsync(StateHasChanged);
```

4. In `Dispose()`, add:
```csharp
FavSvc.OnChanged -= OnFavoritesChanged;
```

5. Add `OnAfterRenderAsync` override (after `OnParametersSetAsync`):
```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (!firstRender) return;
    await FavSvc.LoadAsync();
    StateHasChanged();
}
```

- [ ] **Step 2: Add heart button next to game title**

In `GamePage.razor`, locate `<h1 class="text-2xl font-bold mb-1">@_game.Title</h1>`. Replace that line with:

```razor
<div class="flex items-center gap-3 mb-1">
    <h1 class="text-2xl font-bold">@_game.Title</h1>
    <button class="bg-white rounded-full w-8 h-8 flex items-center justify-center shadow-sm p-1.5 flex-shrink-0
                   @(FavSvc.IsFavorite(_game.Slug) ? "text-rose-500" : "text-slate-300")"
            @onclick="() => _ = FavSvc.ToggleAsync(_game.Slug)"
            title="@(FavSvc.IsFavorite(_game.Slug) ? "Remove favourite" : "Add to favourites")">
        @if (FavSvc.IsFavorite(_game.Slug))
        {
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="w-full h-full">
                <path d="m11.645 20.91-.007-.003-.022-.012a15.247 15.247 0 0 1-.383-.218 25.18 25.18 0 0 1-4.244-3.17C4.688 15.36 2.25 12.174 2.25 8.25 2.25 5.322 4.714 3 7.688 3A5.5 5.5 0 0 1 12 5.052 5.5 5.5 0 0 1 16.313 3c2.973 0 5.437 2.322 5.437 5.25 0 3.925-2.438 7.111-4.739 9.256a25.175 25.175 0 0 1-4.244 3.17 15.247 15.247 0 0 1-.383.218l-.022.012-.007.003-.003.001a.752.752 0 0 1-.704 0l-.003-.001Z" />
            </svg>
        }
        else
        {
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-full h-full">
                <path stroke-linecap="round" stroke-linejoin="round" d="M21 8.25c0-2.485-2.099-4.5-4.688-4.5-1.935 0-3.597 1.126-4.312 2.733-.715-1.607-2.377-2.733-4.313-2.733C5.1 3.75 3 5.765 3 8.25c0 7.22 9 12 9 12s9-4.78 9-12Z" />
            </svg>
        }
    </button>
</div>
```

- [ ] **Step 3: Build to verify no compile errors**

```
cd webapp && dotnet build -v minimal
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add webapp/Components/Pages/GamePage.razor
git commit -m "feat: add heart toggle button to GamePage title"
```

---

## Task 7: Favorites.razor — new page

**Files:**
- Create: `webapp/Components/Pages/Favorites.razor`

- [ ] **Step 1: Create Favorites.razor**

Create `webapp/Components/Pages/Favorites.razor`:

```razor
@page "/favorites"
@rendermode InteractiveServer
@inject FavoritesService FavSvc
@inject GameService GameSvc
@inject LanguageService LangSvc
@implements IDisposable

<div class="mb-6">
    <h2 class="text-xl font-bold">@Strings.Get(LangSvc.Current, "favorites")</h2>
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
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"
             class="w-12 h-12 mx-auto mb-4">
            <path stroke-linecap="round" stroke-linejoin="round" d="M21 8.25c0-2.485-2.099-4.5-4.688-4.5-1.935 0-3.597 1.126-4.312 2.733-.715-1.607-2.377-2.733-4.313-2.733C5.1 3.75 3 5.765 3 8.25c0 7.22 9 12 9 12s9-4.78 9-12Z" />
        </svg>
        <p>@Strings.Get(LangSvc.Current, "no_favorites")</p>
    </div>
}
else
{
    <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-3 xl:grid-cols-4 gap-6">
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
                    <button class="absolute top-2 right-2 bg-white rounded-full w-7 h-7 flex items-center justify-center shadow-sm p-1.5 text-rose-500"
                            @onclick="() => OnRemove(game.Slug)"
                            @onclick:stopPropagation
                            title="Remove favourite">
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="w-full h-full">
                            <path d="m11.645 20.91-.007-.003-.022-.012a15.247 15.247 0 0 1-.383-.218 25.18 25.18 0 0 1-4.244-3.17C4.688 15.36 2.25 12.174 2.25 8.25 2.25 5.322 4.714 3 7.688 3A5.5 5.5 0 0 1 12 5.052 5.5 5.5 0 0 1 16.313 3c2.973 0 5.437 2.322 5.437 5.25 0 3.925-2.438 7.111-4.739 9.256a25.175 25.175 0 0 1-4.244 3.17 15.247 15.247 0 0 1-.383.218l-.022.012-.007.003-.003.001a.752.752 0 0 1-.704 0l-.003-.001Z" />
                        </svg>
                    </button>
                </div>
                <h3 class="font-semibold text-center text-sm md:text-base group-hover:text-indigo-600 truncate">
                    @game.Title
                </h3>
                <p class="text-[11px] text-slate-400 text-center mt-0.5 truncate">@game.Company</p>
            </a>
        }
    </div>
}

@code {
    private List<Game> _games = [];
    private bool _loading = true;

    protected override void OnInitialized()
    {
        LangSvc.OnChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        LangSvc.OnChanged -= OnLanguageChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        await FavSvc.LoadAsync();
        _games = await GameSvc.GetGamesBySlugsAsync(FavSvc.GetSlugs());
        _loading = false;
        StateHasChanged();
    }

    private async Task OnRemove(string slug)
    {
        await FavSvc.ToggleAsync(slug);
        _games = _games.Where(g => FavSvc.IsFavorite(g.Slug)).ToList();
    }
}
```

- [ ] **Step 2: Build to verify no compile errors**

```
cd webapp && dotnet build -v minimal
```

Expected: Build succeeded.

- [ ] **Step 3: Run full test suite**

```
cd webapp.Tests && dotnet test -v minimal
```

Expected: all tests pass.

- [ ] **Step 4: Commit**

```bash
git add webapp/Components/Pages/Favorites.razor
git commit -m "feat: add Favorites page with localStorage-backed game grid"
```
