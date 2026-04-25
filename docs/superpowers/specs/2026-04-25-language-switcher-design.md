# Language Switcher Design

**Date:** 2026-04-25  
**Scope:** Add Thai/English language switcher to KiddoGame Blazor webapp

---

## Overview

Add a language switcher to the top-right of the header. Default language is detected from the browser locale (Thai if `navigator.language` starts with `"th"`, otherwise English). Preference is persisted in `localStorage`. Switching language affects all UI strings across the app and the game description/instruction content on the game page.

---

## Architecture

**Approach:** Scoped `LanguageService` + `CascadingValue`

A C# service holds the current language and notifies subscribers on change. `MainLayout` wraps `@Body` in a `CascadingValue` so all pages receive the language automatically. JS interop handles locale detection and localStorage persistence.

---

## New Files

### `Models/Lang.cs`
```csharp
public enum Lang { Thai, English }
```

### `Services/LanguageService.cs`
- Scoped DI service
- `Lang Current` property
- `void SetLanguage(Lang lang)` — updates `Current`, fires `OnChanged`
- `event Action? OnChanged`

### `Services/Strings.cs`
- Static class
- `static string Get(Lang lang, string key, params object[] args)` — returns Thai or English string; if `args` are provided, result is passed through `string.Format`
- Keys cover all hardcoded UI labels in Home, GamePage, and MainLayout

---

## Modified Files

### `Models/Game.cs`
Add two nullable fields:
- `string? DescriptionTh`
- `string? InstructionTh`

### `Kiddo.Web.csproj`
Add NuGet reference:
```xml
<PackageReference Include="Blazor.Flags" Version="1.0.0.1" />
```

### `Components/_Imports.razor`
Add:
```razor
@using Blazor.Flags
```

### `Components/App.razor`
Add JS helpers before `</body>`:
```js
window.getLang = function() {
    var stored = localStorage.getItem("lang");
    if (stored) return stored;
    return navigator.language.startsWith("th") ? "th" : "en";
};
window.setLang = function(lang) {
    localStorage.setItem("lang", lang);
};
```

### `Program.cs`
Register service:
```csharp
builder.Services.AddScoped<LanguageService>();
```

### `Components/Layout/MainLayout.razor`
- Inject `IJSRuntime` and `LanguageService`
- `@implements IDisposable` — subscribe to `LanguageService.OnChanged` in `OnInitialized`, unsubscribe in `Dispose()` to avoid memory leaks
- `OnChanged` handler calls `InvokeAsync(StateHasChanged)` (thread-safe)
- In `OnAfterRenderAsync` (first render only): call `window.getLang()`, call `LanguageService.SetLanguage()`
- Wrap `@Body` in `<CascadingValue Value="LangSvc">`
- Add language switcher to header right side:
  - Button: `<CountryFlag Country="Country.TH/GB" Size="FlagSize.Small" />` + chevron
  - Dropdown: two rows with flag + name, active language highlighted in indigo
  - Backdrop div to close on outside click

### `Components/Pages/Home.razor`
- Add `[CascadingParameter] LanguageService LangSvc { get; set; }`
- Replace all hardcoded Thai strings with `Strings.Get(LangSvc.Current, "key")`
- Strings to cover: search placeholder, "ทั้งหมด" category label, section title, pagination labels, loading text, empty state text

### `Components/Pages/GamePage.razor`
- Add `[CascadingParameter] LanguageService LangSvc { get; set; }`
- Description: show `_game.DescriptionTh` if Thai and non-null, else `_game.Description`
- Instruction: show `_game.InstructionTh` if Thai and non-null, else `_game.Instruction`
- Replace hardcoded labels ("วิธีเล่น", "เกมที่คล้ายกัน", "โดย", loading, not-found) via `Strings`

---

## UI Layout

```
┌─────────────────────────────────────────────────┐
│ KiddoGame                            [🇹🇭 ▾]    │
│ เล่นสนุก เรียนรู้ไว 🌈                           │
└─────────────────────────────────────────────────┘
                                    ┌─────────────┐
                                    │ 🇹🇭 ภาษาไทย │  ← active (indigo bg)
                                    │ 🇬🇧 English  │
                                    └─────────────┘
```

---

## Data Flow

```
App starts
  → MainLayout renders (Thai shown during SSR — default)
  → OnAfterRenderAsync fires (first render only)
  → JS: window.getLang() reads localStorage or navigator.language
  → LanguageService.SetLanguage() called
  → OnChanged fires → MainLayout.StateHasChanged()
  → CascadingValue propagates Lang to Home / GamePage
  → Components re-render with correct language
```

---

## Content Fallback

If Thai is selected but `DescriptionTh` or `InstructionTh` is null, fall back to the English field. This covers games not yet translated.

---

## Strings Coverage

| Key | Thai | English |
|-----|------|---------|
| `search_placeholder` | ค้นหาเกมที่อยากเล่น... | Search for a game... |
| `all_categories` | 🌟 ทั้งหมด | 🌟 All |
| `all_games` | เกมทั้งหมด | All Games |
| `items_count` | {n} รายการ | {n} items |
| `loading` | กำลังโหลด... | Loading... |
| `no_results` | ไม่พบเกมที่คุณค้นหา ลองเปลี่ยนคำดูนะ | No games found. Try a different search. |
| `prev_page` | ← ก่อนหน้า | ← Prev |
| `next_page` | ถัดไป → | Next → |
| `page_of` | หน้า {p} / {t} | Page {p} / {t} |
| `by_company` | โดย | by |
| `how_to_play` | วิธีเล่น | How to play |
| `similar_games` | เกมที่คล้ายกัน | Similar games |
| `game_not_found` | ไม่พบเกมนี้ | Game not found |
| `back_home` | ← กลับหน้าแรก | ← Back to home |
| `tagline` | เล่นสนุก เรียนรู้ไว 🌈 | Play & Learn 🌈 |

---

## Out of Scope

- More than 2 languages
- Server-side locale detection / cookies
- Translating game `Title`, `Company`, or `Categories`
- Unit tests (logic is simple enum switch + string lookup)
