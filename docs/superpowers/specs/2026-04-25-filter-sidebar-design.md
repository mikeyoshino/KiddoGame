# Filter Sidebar Design

**Date:** 2026-04-25
**Status:** Approved

## Overview

Replace the current horizontal category-pill filter on the Home page with a left-side filter sidebar. On desktop the sidebar is always visible; on mobile/tablet it is hidden off-screen and slides in from the left when the user taps an arrow tab pinned to the left edge.

The sidebar contains a single filter section: **Genre** (backed by the existing `categories` array on the `Game` model).

---

## Architecture

### New component: `FilterSidebar.razor`

A self-contained Blazor component that owns its open/closed state for mobile. It receives the list of available genres with counts and the currently selected genres, and emits a callback when the selection changes.

```
FilterSidebar
  Props:
    - List<(string Genre, int Count)> Genres
    - HashSet<string> SelectedGenres
    - EventCallback<HashSet<string>> OnSelectionChanged
```

The parent (`Home.razor`) passes data down and reacts to `OnSelectionChanged` by reloading the game list.

### Layout change: `Home.razor`

The existing horizontal pills section is removed. The page body becomes a two-column flex row:

```
[FilterSidebar (w-64, shrink-0)]  [Game Area (flex-1)]
```

On screens narrower than `lg` (1024 px), the sidebar column is removed from normal flow and the `FilterSidebar` renders as a fixed drawer instead.

### Service change: `GameService.GetGamesAsync`

Add support for filtering by multiple categories (OR logic — games matching any selected genre are included). The PostgREST query uses the `ov` (overlaps) operator with an array literal:

```
&categories=ov.{Genre1,Genre2}
```

Signature change:
```csharp
// Before
Task<(List<Game>, int)> GetGamesAsync(int page, int pageSize, string? category, string? search)

// After
Task<(List<Game>, int)> GetGamesAsync(int page, int pageSize, IReadOnlySet<string>? categories, string? search)
```

### Category counts

`GetCategoriesAsync` currently returns a plain `List<string>`. A new Supabase RPC (or client-side count from existing data) must return genre + count pairs. The cleanest approach: extend the existing `get_distinct_categories` RPC to return `{ category, count }` rows, matching what the scraper already has in the DB.

---

## Component Design

### `FilterSidebar.razor` internals

**State:**
- `bool _genreOpen = true` — Genre section starts expanded
- `string _search = ""` — genre search box value
- `bool _drawerOpen = false` — mobile drawer visibility (toggled by arrow tab / overlay click)

**Genre section:**
- Header button: toggles `_genreOpen`, shows chevron up/down
- Search input: filters the displayed genre list client-side (no server round-trip)
- Scrollable list of checkboxes: each shows genre name + count badge
- Selecting/deselecting a checkbox updates `SelectedGenres` and fires `OnSelectionChanged` immediately (no Apply button)

**Mobile behaviour:**
- When `_drawerOpen = false`: sidebar is `transform: translateX(-100%)`, fixed positioned, z-index above content. An arrow tab (`›`) is pinned at `left: 0`, `top: 50%`, always visible.
- When `_drawerOpen = true`: sidebar slides to `translateX(0)`. A semi-transparent overlay covers the game area; clicking it sets `_drawerOpen = false`.
- Transition: `transition: transform 300ms ease-in-out` on the sidebar element.
- No JS interop required — CSS transitions + Blazor bool toggle.

**Desktop behaviour:**
- Sidebar is `position: static`, `width: 256px`, always visible. Arrow tab and overlay are hidden via `hidden lg:block` / `lg:hidden` Tailwind classes.

---

## Data Flow

```
Home.razor (page)
  ├── _selectedGenres: HashSet<string>    ← filter state
  ├── _genres: List<(string, int)>        ← loaded once on init
  │
  ├── FilterSidebar
  │     ← Genres, SelectedGenres
  │     → OnSelectionChanged → Home updates _selectedGenres, resets page to 1, calls LoadGames()
  │
  └── Game grid + pagination
        ← _games, _loading, _page, _totalPages
```

---

## CSS / Styling

All styling via Tailwind utility classes, consistent with the existing codebase. Key classes for the drawer:

```css
/* sidebar wrapper */
fixed top-0 left-0 h-full z-30
lg:static lg:h-auto lg:z-auto

/* transform toggle */
-translate-x-full   ← when _drawerOpen = false
translate-x-0       ← when _drawerOpen = true
transition-transform duration-300 ease-in-out

/* arrow tab */
fixed left-0 top-1/2 -translate-y-1/2
w-6 h-12 bg-indigo-500 rounded-r-xl
flex items-center justify-center
text-white z-40 lg:hidden

/* overlay */
fixed inset-0 bg-black/25 z-20 lg:hidden
```

---

## Out of Scope

- Players filter (removed by user)
- Mobile Compatible filter (removed by user)
- Language filter
- Any changes to the GamePage detail view
- Apply / Reset buttons (instant filter)
