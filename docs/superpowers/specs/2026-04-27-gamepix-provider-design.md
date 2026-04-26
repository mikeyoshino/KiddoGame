# GamePix Provider Integration — Design Spec

**Date:** 2026-04-27  
**Status:** Approved

## Overview

Add GamePix as a second game provider alongside GameDistribute (GD). Games from both providers share the same `games` table. A new Python scraper (`gamepix_main.py`) fetches all GamePix games via their JSON feed, translates descriptions inline via OpenAI, and posts to the existing webapp ingest API. The webapp handles thumbnail download and Supabase upsert for both providers.

---

## 1. Database

New SQL migration (`003_add_provider_fields.sql`):

```sql
ALTER TABLE games
  ADD COLUMN IF NOT EXISTS provider          TEXT NOT NULL DEFAULT 'GameDistribute',
  ADD COLUMN IF NOT EXISTS provider_game_id  TEXT,
  ADD COLUMN IF NOT EXISTS game_url          TEXT;

-- Backfill all existing GD records
UPDATE games SET provider = 'GameDistribute' WHERE provider IS NULL OR provider = '';
```

- `provider` — `'GameDistribute'` or `'GamePix'`
- `provider_game_id` — raw numeric ID from GamePix feed (stored as TEXT); null for existing GD records
- `game_url` — play URL provided by GamePix; null for GD records (URL is computed from `object_id`)
- `object_id` remains the unique key; GamePix games use `gp_{id}` prefix (e.g. `gp_12345`) to guarantee no collision with GD hex UUIDs

---

## 2. Python Scraper

### `scraper/gamepix_client.py`

Fetches all pages from the GamePix JSON feed until `next_url` is absent. Feed URL: `https://feeds.gamepix.com/v2/json?sid=22322&pagination=50&page={n}`

Field mapping:

| GamePix feed field | DB column / ingest field |
|---|---|
| `"gp_" + item.id` | `object_id` |
| `str(item.id)` | `provider_game_id` |
| `item.namespace` | `slug` |
| `item.title` | `title` |
| `item.description` | `description` |
| `null` | `instruction` (GamePix has none) |
| `item.banner_image` | `thumbnail_url` |
| `item.url` | `game_url` |
| `[item.category]` | `categories` (single → one-element array) |
| `item.date_published` | `first_active_date` |
| `"GamePix"` | `provider` |

### `scraper/gamepix_main.py`

Orchestrates the full scrape:

1. **Resume checkpoint** — reads `scraper/gamepix_progress.json` on startup (`{"last_completed_page": N}`). If present, starts from page `N+1`. Deletes file on full completion.
2. **Pagination** — iterates all pages from feed until `next_url` absent.
3. **Dedup by object_id** — calls existing `filter_new` (webapp `/api/ingest/filter-new`). Skips games already in DB as `done`.
4. **Dedup by title** — calls new `check_title_duplicates` (webapp `/api/ingest/check-title-duplicates`). Skips any GamePix game whose title already exists in the DB from any provider.
5. **Translation** — calls OpenAI directly (reuses logic from `translate.py`). Only `description_th` is translated (`instruction` is always null for GamePix). If OpenAI fails for any batch: saves those games with `description_th = null` / `translation_status = null` and **stops the script**. Already-processed games remain saved; `translate.py` can pick up the null translations later.
6. **Post to webapp** — posts pre-translated games (batch of 10) to `/api/ingest/batch`. Webapp downloads thumbnail and upserts; skips its own translation step because `translation_status` is pre-set.
7. **Progress save** — writes `gamepix_progress.json` after each page completes successfully.

### `scraper/webapp_client.py`

Add one new function:
```python
async def check_title_duplicates(titles: list[str]) -> list[str]:
    """Return titles that already exist in the DB."""
```

---

## 3. Webapp (C#)

### `Models/IngestGame.cs`

Add new optional fields to the `IngestGame` record (all default to null / "GameDistribute" so GD scraping is unaffected):

```csharp
string Provider = "GameDistribute",
string? ProviderGameId = null,
string? GameUrl = null,
string? DescriptionTh = null,
string? InstructionTh = null,
string? TranslationStatus = null
```

### `Models/Game.cs`

- Add `Provider`, `ProviderGameId` properties (mapped from DB columns)
- Rename existing computed `GameUrl` property to `ComputedGameUrl` (or make it a fallback)
- Add stored `GameUrl` property; update the play-URL logic: return stored `GameUrl` if set, else fall back to the existing GD formula: `https://html5.gamedistribution.com/{ObjectId}/?gd_sdk_referrer_url=...`

### `Services/IngestService.cs`

**`UpsertGamesAsync`** — store the three new columns (`provider`, `provider_game_id`, `game_url`). If `TranslationStatus` is pre-set on the incoming game, use it directly and skip calling `TranslateBatchAsync` for that game. GD games have `TranslationStatus = null` so their behaviour is unchanged.

**New method + endpoint:**
```csharp
// POST /api/ingest/check-title-duplicates
// Body: string[] titles
// Returns: string[] (titles that already exist in DB)
public async Task<string[]> CheckTitleDuplicatesAsync(string[] titles)
```

Queries Supabase: `SELECT title FROM games WHERE title IN (...)` and returns matched titles.

---

## 4. Data Flow

```
gamepix_main.py
  → gamepix_client.py (fetch page)
  → webapp /api/ingest/filter-new (skip done object_ids)
  → webapp /api/ingest/check-title-duplicates (skip title collisions)
  → OpenAI (translate description → description_th)
  → webapp /api/ingest/batch (thumbnail download + upsert)
  → gamepix_progress.json (save checkpoint)
```

---

## 5. Error Handling

| Scenario | Behaviour |
|---|---|
| GamePix feed HTTP error | Log and retry up to 3×, then skip page |
| Thumbnail download fails | Webapp saves game as `status=pending`; re-run picks it up via `filter_new` |
| OpenAI translation fails | Save games with `translation_status=null`, stop script; `translate.py` handles later |
| Title duplicate found | Skip that GamePix game silently (log it) |
| Script interrupted mid-page | Re-run resumes from last completed page via `gamepix_progress.json` |

---

## 6. Out of Scope

- No changes to the GD scraper or its flow
- No UI changes (provider field not displayed yet)
- `quality_score`, `orientation`, `width`, `height` from GamePix feed are not stored (not needed by webapp)
