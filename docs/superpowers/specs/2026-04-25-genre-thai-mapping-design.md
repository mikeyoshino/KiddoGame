# Genre Thai Translation Design

## Problem

Game genres/categories are stored in the database as English strings (e.g. `"Action"`, `"Puzzle"`). When the UI is in Thai mode, genres should display in Thai. Queries must always use the original English values — no database changes.

## Approach

A new static class `GenreTranslations` mirrors the existing `Strings.cs` pattern: a `Dictionary<string, string>` maps English genre keys to Thai display names. A `Translate(string englishKey, Lang lang)` method returns the Thai name when available, falling back to the English key when the language is English or no mapping exists.

The English key is always preserved in `SelectedGenres` and all API calls. Translation is display-only.

## Components

### `webapp/Services/GenreTranslations.cs` (new)

```
static Dictionary<string, string> _thaiMap  // English → Thai
static string Translate(string englishKey, Lang lang)
  → returns Thai if lang==Thai && key found in map
  → otherwise returns englishKey unchanged
```

Genre mapping covers all known GameDistribution categories:
Action, Adventure, Arcade, Board, Car, Card, Casino, Casual, Clicker, Cooking, Educational, Endless Runner, Girls, Horror, Hypercasual, Idle, IO, Kids, Match3, Multiplayer, Music, Platform, Puzzle, Racing, Role Playing, Shooting, Simulation, Soccer, Sports, Strategy, Tower Defense, Word — plus any additional genres that appear unmapped fall back to English automatically.

### `FilterSidebar.razor` (modified)

- Add `[Parameter] public Lang Lang { get; set; }`
- Display label: `GenreTranslations.Translate(genre, Lang)` replaces raw `@g`
- Genre search `FilteredGenres`: filter by `GenreTranslations.Translate(g.Genre, Lang).Contains(_search, ...)` so search works against the active language's display text
- `SelectedGenres` and `OnSelectionChanged` callbacks unchanged — English keys only

### `Home.razor` (modified)

- Pass current lang to sidebar: `<FilterSidebar Lang="LangSvc.Current" ...>`
- Selected genre heading: `string.Join(", ", _selectedGenres.Select(g => GenreTranslations.Translate(g, LangSvc.Current)))` replaces raw English join

### `GamePage.razor` (modified)

- Category badge: `GenreTranslations.Translate(cat, LangSvc.Current)` replaces raw `@cat`

## Data Flow

```
DB stores: ["Action", "Puzzle"]
           ↓
GetCategoriesAsync() → List<(string Genre, int Count)>  ← English throughout
           ↓
FilterSidebar displays: GenreTranslations.Translate(genre, lang)  ← Thai or English
           ↓
User selects genre → SelectedGenres = {"Action"}  ← always English
           ↓
API query: categories=ov.{Action}  ← always English
```

## Error Handling

No error handling needed. `Translate()` has no failure mode — unknown genres pass through as-is. New genres added to the DB that lack a mapping will display in English automatically.

## Testing

- Toggle language between Thai and English; genre list in FilterSidebar should switch
- Genre search in Thai mode should match Thai text (e.g. "ปริ" matches "Puzzle")
- Selecting a genre filter in Thai mode must still return correct game results (proves English key is passed to API)
- GamePage category badges should display Thai when Thai mode active
- Home heading with selected genres should display Thai names
