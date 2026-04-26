# Scraper → Webapp Ingest API Design

**Date:** 2026-04-26
**Status:** Approved

## Problem

The scraper runs on a local Mac; the webapp runs on a remote VPS. The current architecture writes directly to Supabase and saves thumbnail images to `webapp/wwwroot/` on the local filesystem — both impossible when the two machines are separate.

## Goal

The scraper becomes a thin data collector. The webapp owns all writes: database, thumbnails, and Thai translation.

## Architecture

```
Mac (scraper)                          VPS (webapp)
─────────────────────────────────      ─────────────────────────────────
main.py
  └─ scrape pages in reverse (N→1)
      for each page:
        ├─ parse hits (gd_client)
        ├─ POST /api/ingest/filter-new
        │   ◄── new + pending ids only
        ├─ fetch_details_batch (unchanged)
        └─ POST /api/ingest/batch (10 games)
                                         1. download thumbnails → wwwroot/images/games/
                                         2. call OpenAI → description_th, instruction_th
                                         3. upsert games to Supabase
                                         ◄── per-game ok/fail
```

## API Endpoints

### `POST /api/ingest/filter-new`

Returns IDs that need processing: either not in DB, or in DB with `status = pending`.

**Request:**
```json
["objectId1", "objectId2", "objectId3"]
```

**Response:**
```json
["objectId2"]
```

### `POST /api/ingest/batch`

Receives up to 10 games. Downloads thumbnails, translates, upserts to Supabase.

**Request:**
```json
{
  "games": [
    {
      "object_id": "abc123",
      "slug": "cool-game",
      "title": "Cool Game",
      "company": "Acme",
      "thumbnail_url": "https://img.gamedistribution.com/abc123-512x384.jpg",
      "description": "...",
      "instruction": "...",
      "categories": ["action"],
      "tags": ["fun"],
      "languages": ["en"],
      "gender": ["boy"],
      "age_group": ["8-12"]
    }
  ]
}
```

**Response:**
```json
{
  "results": [
    { "object_id": "abc123", "ok": true },
    { "object_id": "def456", "ok": false, "error": "thumbnail: all extensions failed" }
  ]
}
```

**Endpoint steps (in order):**
1. Download all thumbnails concurrently, trying extensions in order: `.jpg`, `.jpeg`, `.png`, `.webp`, `.gif`
2. Call OpenAI GPT-4o-mini with full batch → get `description_th` + `instruction_th`
3. Upsert each game to Supabase:
   - Thumbnail OK → `status = done`, local path stored in `thumbnail_url` (e.g. `/images/games/abc123.jpg`)
   - Thumbnail fail → `status = pending`, original remote URL kept in `thumbnail_url`
   - Translation fail (per-game or entire batch OpenAI failure) → `status = done`, `description_th`/`instruction_th` left null — `translate.py` can back-fill these manually

## Ordering

Games are scraped in reverse page order (page N → page 1). GD page 1 holds the newest games. By inserting them last, they receive the most recent Supabase auto-`created_at` and appear first in the webapp (`ORDER BY created_at DESC`).

For incremental runs (most common case), new games are always on page 1 of GD and receive `NOW()` on insert — automatically more recent than all existing records.

No synthetic timestamps or additional sort columns are needed.

## Retry Logic

The `filter-new` endpoint returns both unknown IDs and IDs with `status = pending`. This means the scraper naturally retries failed games on every run without any explicit retry sequence at startup.

## Scraper Changes

### New file: `webapp_client.py`
```python
async def filter_new(object_ids: list[str]) -> list[str]: ...
async def post_batch(games: list[dict]) -> list[dict]: ...
```

### `main.py`
Simplified to: scrape pages in reverse → filter-new per page → fetch details → post_batch in groups of 10.

Removed: `retry_pending()`, `repair_missing_thumbnails()`, `mark_missing_local_thumbnails_pending()`, `mark_null_thumbnails_pending()`.

### `config.py`
**Removed:** `SUPABASE_URL`, `SUPABASE_KEY`, `THUMBNAIL_OUTPUT_DIR`, `THUMBNAIL_URL_PREFIX`, `THUMBNAIL_CONCURRENCY`, `THUMBNAIL_DELAY`

**Added:** `WEBAPP_URL`

The scraper must use a long HTTP timeout for `POST /api/ingest/batch` (recommended: 120s) to accommodate concurrent thumbnail downloads + synchronous OpenAI call for 10 games.

### Files kept (standalone utilities, not called from `main.py`)
- `db.py` — manual DB queries
- `translate.py` — manual re-translation runs
- `thumbnail_downloader.py` — manual thumbnail repair

## Webapp Changes

### `Program.cs`
Two new minimal API endpoints registered.

### New service: `IngestService.cs`
Handles thumbnail download, OpenAI translation, and Supabase upsert. Injected into the endpoints.

### `appsettings.json` additions
```json
"Ingest": {
  "OpenAiApiKey": "sk-...",
  "ThumbnailDir": "wwwroot/images/games",
  "ThumbnailUrlPrefix": "/images/games"
}
```

The existing `Supabase:Key` (anon key) is used by `GameService` for reads. `IngestService` needs a separate `Supabase:ServiceKey` (service role key) for upserts — add this to `appsettings.json`.

## Config Summary

| | Before | After |
|---|---|---|
| Scraper needs | `SUPABASE_URL`, `SUPABASE_KEY`, filesystem path | `WEBAPP_URL` only |
| Webapp owns | Static file serving | Static file serving + DB writes + thumbnails + translation |
| Retry mechanism | Explicit startup sequence | Implicit via `filter-new` returning pending IDs |
| Translation | Separate manual script | Integrated into batch ingest (sync, per 10 games) |
