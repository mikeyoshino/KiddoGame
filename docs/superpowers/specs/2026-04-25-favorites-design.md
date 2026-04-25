# Favorites Feature Design

## Overview

Allow users to save favourite games without requiring login. Favourites are stored in `localStorage` and persist across sessions on the same device.

---

## Architecture

### FavoritesService (new scoped C# service)

`webapp/Services/FavoritesService.cs`

- **State:** `HashSet<string> _slugs` — in-memory cache of favourite game slugs.
- **`LoadAsync()`** — reads `"kiddo_favorites"` key from `localStorage` (JSON array of slugs). Called once in `OnAfterRenderAsync(firstRender)` by any component that needs favourites state. Safe to call multiple times (no-ops after first load).
- **`ToggleAsync(slug)`** — adds or removes the slug from `_slugs`, serialises back to `localStorage`, fires `OnChanged`.
- **`IsFavorite(slug)`** — synchronous check against in-memory set.
- **`event Action OnChanged`** — mirrors `LanguageService.OnChanged`. Subscribed components call `InvokeAsync(StateHasChanged)` on fire.
- **JS interop** — uses `IJSRuntime` to call `localStorage.getItem` / `localStorage.setItem`.
- Registered as **scoped** in `Program.cs` (one instance per SignalR circuit).

### GameService (addition)

`webapp/Services/GameService.cs`

- **`GetGamesBySlugsAsync(string[] slugs)`** — queries `games?slug=in.(slug1,slug2,...)`. Used by the Favourites page to load all saved games in a single request. Returns `List<Game>`. Returns empty list if `slugs` is empty.

---

## Components

### Modified

| Component | Change |
|---|---|
| `Home.razor` | Inject `FavoritesService`. In `OnAfterRenderAsync(firstRender)` call `LoadAsync()` and subscribe to `OnChanged`. Add heart button (Heroicon SVG) positioned `absolute top-2 right-2` inside each game card thumbnail div. Click calls `ToggleAsync(game.Slug)` with `@onclick:stopPropagation`. |
| `GamePage.razor` | Same inject + subscribe pattern. Add heart button (Heroicon SVG, larger — `w-6 h-6`) next to the game title in the info panel. |
| `TopNav.razor` | Add "Favorites" (`รายการโปรด`) nav link pointing to `/favorites`. Plain text, no icon. Same active-highlight logic as "Home". |
| `MobileNav.razor` | Add Heroicon heart (outline) nav item for `/favorites` between Home and Search. Active when on `/favorites`. |
| `Strings.cs` | Add keys: `favorites` ("รายการโปรด" / "Favorites"), `no_favorites` ("ยังไม่มีเกมโปรด" / "No favorites yet. Start adding some!"). |

### New

**`webapp/Components/Pages/Favorites.razor`** — page at `/favorites`.

- `@rendermode InteractiveServer`
- Injects `FavoritesService`, `GameService`, `LanguageService`.
- `OnAfterRenderAsync(firstRender)`: loads favourites, fetches games via `GetGamesBySlugsAsync`, renders grid.
- Same grid markup as `Home.razor` (2/3/4 col responsive, game card with thumbnail, title, company).
- Heart button on each card (same as Home) so user can un-favourite inline.
- Empty state: centred message + Heroicon heart outline icon when `_slugs` is empty.
- No pagination (typical user has < 30 favourites; can add later if needed).
- Subscribes to `FavoritesService.OnChanged` and `LanguageService.OnChanged` for re-renders.

---

## Heart Button Design

- **White circular button**, `w-7 h-7`, `rounded-full`, `shadow-sm`, positioned `absolute top-2 right-2` over the thumbnail.
- **Favourited:** Heroicon solid heart (`fill="currentColor"`), `text-rose-500`.
- **Not favourited:** Heroicon outline heart (`stroke="currentColor"`), `text-slate-300`.
- `@onclick:stopPropagation` prevents the card's `<a>` navigation from firing.

---

## Data Flow

```
OnAfterRenderAsync(firstRender)
  └─ FavoritesService.LoadAsync()
       └─ JSRuntime: localStorage.getItem("kiddo_favorites")
            └─ deserialise JSON → _slugs HashSet

User clicks ♥
  └─ FavoritesService.ToggleAsync(slug)
       ├─ update _slugs
       ├─ JSRuntime: localStorage.setItem("kiddo_favorites", json)
       └─ OnChanged.Invoke()
            └─ subscribed components → StateHasChanged
```

---

## localStorage Format

```json
["slug-one", "slug-two", "slug-three"]
```

Key: `"kiddo_favorites"`. Missing key treated as empty list.

---

## Strings

| Key | Thai | English |
|---|---|---|
| `favorites` | รายการโปรด | Favorites |
| `no_favorites` | ยังไม่มีเกมโปรด เพิ่มได้เลย! | No favorites yet. Start adding some! |

---

## Testing

- **`FavoritesServiceTests`** — new xUnit test class using a fake `IJSRuntime`. Tests: `LoadAsync` parses JSON correctly, `LoadAsync` handles missing key (returns empty), `ToggleAsync` adds slug, `ToggleAsync` removes already-present slug, `IsFavorite` returns correct state, `OnChanged` fires on toggle.
- **`GameServiceTests`** — add `BuildGamesUrl_BySlugs_UsesInOperator` and `GetGamesBySlugsAsync_ReturnsEmpty_WhenNoSlugs`.

---

## Out of Scope

- Sync favourites across devices (requires login).
- Sorting / filtering on the Favourites page.
- Pagination on the Favourites page.
