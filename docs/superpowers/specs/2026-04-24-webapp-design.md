# Kiddo Webapp Design
**Date:** 2026-04-24  
**Project:** Kiddo — HTML5 Games Aggregator  
**Scope:** Blazor Server web application that displays scraped games from Supabase

---

## Overview

Kiddo webapp displays HTML5 games scraped from gamedistribution.com. Users browse games by category, search by title, and play games embedded via iframe. The UI follows the KiddoGame design template: Thai language, Tailwind CSS, Kanit font, mobile-first.

---

## Project Structure

```
Kiddo/
├── scraper/          ← Python scraper (existing)
├── webapp/           ← Blazor Server app (this project)
│   ├── Components/
│   │   ├── Layout/
│   │   │   ├── MainLayout.razor
│   │   │   └── MobileNav.razor
│   │   ├── Pages/
│   │   │   ├── Home.razor          ← game grid, search, category filter
│   │   │   └── GamePage.razor      ← iframe + info + similar games
│   │   └── App.razor
│   ├── Services/
│   │   └── GameService.cs          ← all Supabase queries
│   ├── Models/
│   │   └── Game.cs                 ← maps to games table
│   ├── wwwroot/
│   │   └── app.css                 ← minimal, Tailwind via CDN
│   ├── Program.cs
│   └── webapp.csproj
└── docs/
```

---

## Tech Stack

| Concern | Choice |
|---------|--------|
| Framework | Blazor Server, .NET 8 LTS |
| CSS | Tailwind CSS via CDN |
| Font | Kanit (Google Fonts) |
| Database client | `supabase-csharp` |
| Language | Thai (UI text) |

---

## Pages

### Home (`/`)

**Layout:**
- Sticky header: KiddoGame logo + search input
- Category pills: horizontal scrollable row, populated from distinct categories in DB. "ทั้งหมด" (All) pill always first
- Game grid: 2 cols mobile → 3 cols sm → 4 cols lg → 6 cols xl
- Each game card: thumbnail image, title, company
- Pagination: 30 games per page, previous/next buttons + page number
- Mobile bottom nav: Home | Search (fixed, bottom)

**Data:**
- On load: fetch page 1, 30 games, total count (for pagination)
- Category filter: re-fetches from Supabase filtered by category
- Search: re-fetches from Supabase with title ILIKE query (server-side, debounced 300ms)
- Only games with `status = 'done'` are shown

---

### Game (`/games/{slug}`)

**Layout (desktop):**
```
┌─────────────────────────────┬──────────────┐
│  iframe (game)              │ Similar Games│
│                             │ (5 cards)    │
├─────────────────────────────┤              │
│  Title, Company             │              │
│  Description                │              │
│  Instructions               │              │
│  Categories | Tags          │              │
└─────────────────────────────┴──────────────┘
```

**Layout (mobile):**
- iframe (full width)
- Game info below
- Similar games below info (horizontal scroll row)

**Similar games:** query `games` table where `categories && {game.categories}` (array overlap), exclude current game, limit 5, status = 'done'

**iframe URL:** `https://html5.gamedistribution.com/{object_id}/`

---

## Data Model (C#)

```csharp
public class Game
{
    public string Id { get; set; }
    public string ObjectId { get; set; }
    public string Slug { get; set; }
    public string Title { get; set; }
    public string Company { get; set; }
    public string ThumbnailUrl { get; set; }
    public string Description { get; set; }
    public string Instruction { get; set; }
    public string[] Categories { get; set; }
    public string[] Tags { get; set; }
    public string[] Languages { get; set; }
    public string[] Gender { get; set; }
    public string[] AgeGroup { get; set; }
    public string Status { get; set; }
    public int ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public string GameUrl => $"https://html5.gamedistribution.com/{ObjectId}/";
}
```

---

## GameService (Supabase Queries)

```
GetGamesAsync(page, pageSize, category, search) → (List<Game>, int total)
GetGameBySlugAsync(slug) → Game?
GetSimilarGamesAsync(categories[], excludeSlug, limit=5) → List<Game>
GetCategoriesAsync() → List<string>
```

---

## Configuration

```json
// appsettings.json
{
  "Supabase": {
    "Url": "https://xxxx.supabase.co",
    "Key": "your-anon-key"
  }
}
```

Uses the **anon key** (not service role) since the webapp only reads public data.

---

## Design Reference

UI follows `docs/specs/DesignTemplate.html`:
- Color: indigo-500/600 primary, slate-50 background
- Font: Kanit (300, 400, 600)
- Cards: rounded-[2.5rem], hover:-translate-y-2 effect
- Category pills: active state bg-indigo-600 text-white
- Mobile nav: fixed bottom, backdrop-blur
