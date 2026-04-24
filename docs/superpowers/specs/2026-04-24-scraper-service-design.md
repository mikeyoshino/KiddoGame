# Scraper Service Design
**Date:** 2026-04-24  
**Project:** Kiddo — HTML5 Games Aggregator  
**Scope:** Python scraper service that populates a Supabase database with game data from gamedistribution.com

---

## Overview

Kiddo is a web platform that aggregates HTML5 games from gamedistribution.com. This service scrapes game metadata and stores it in Supabase. The web frontend (built separately) will use the stored data to display games and embed them via iframe using the `object_id`.

The game iframe URL is constructed from `object_id`:
```
https://html5.gamedistribution.com/{object_id}/
```

---

## Architecture

```
scraper_service/
├── main.py            # single entry point: python main.py
├── config.py          # env vars (Supabase URL, key, concurrency limit)
├── gd_client.py       # GraphQL listing API (pagination)
├── detail_fetcher.py  # fetch game detail page, parse __NEXT_DATA__
├── db.py              # Supabase inserts/updates/queries
├── requirements.txt
└── .env
```

### Data Sources

| Data | Source |
|------|--------|
| objectID, title, company, slug, assets | GraphQL API (`gd-website-api.gamedistribution.com/graphql`) |
| description, instructions, categories, tags, languages, gender, age_group | `__NEXT_DATA__` JSON embedded in each game's HTML page (`gamedistribution.com/games/{slug}/`) |

---

## Run Flow

Single command: `python main.py`

```
1. Load all existing object_ids from Supabase → Python set (O(1) skip lookup)

2. Retry pending records:
   - SELECT object_id, slug FROM games WHERE status = 'pending'
   - Fetch detail pages concurrently (max 5 at a time)
   - UPDATE status = 'done' on success; leave as 'pending' on failure

3. Page through GraphQL listing (page 1 → ~712, 30 games/page):
   - If objectID in set → skip instantly
   - If new → INSERT as 'pending', fetch detail concurrently, UPDATE to 'done'

Rate limiting: 1s delay between GraphQL pages, max 5 concurrent detail fetches
```

---

## Supabase Schema

**Table: `games`**

| Column | Type | Notes |
|--------|------|-------|
| `id` | `uuid` | primary key, auto-generated |
| `object_id` | `text` | unique — GD's identifier (e.g. `f078134f39634ca78dcd4a8479a314a2`) |
| `slug` | `text` | unique — used for Kiddo site URLs (e.g. `67-clicker`) |
| `title` | `text` | game title |
| `company` | `text` | published by |
| `thumbnail_url` | `text` | `https://img.gamedistribution.com/{object_id}-512x384.jpg` |
| `description` | `text` | full description (from detail page) |
| `instructions` | `text` | how to play (from detail page) |
| `categories` | `text[]` | e.g. `["Casual", "Agility"]` |
| `tags` | `text[]` | e.g. `["idle", "clicker"]` |
| `languages` | `text[]` | supported languages |
| `gender` | `text[]` | e.g. `["Male", "Female"]` |
| `age_group` | `text[]` | e.g. `["Kids", "Teens", "YoungAdults"]` |
| `status` | `text` | `pending` or `done` |
| `view_count` | `integer` | default 0, incremented by web layer on play |
| `created_at` | `timestamptz` | default `now()` |

### SQL Migration

```sql
CREATE TABLE games (
  id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  object_id    TEXT UNIQUE NOT NULL,
  slug         TEXT UNIQUE NOT NULL,
  title        TEXT NOT NULL,
  company      TEXT,
  thumbnail_url TEXT,
  description  TEXT,
  instructions TEXT,
  categories   TEXT[],
  tags         TEXT[],
  languages    TEXT[],
  gender       TEXT[],
  age_group    TEXT[],
  status       TEXT NOT NULL DEFAULT 'pending',
  view_count   INTEGER NOT NULL DEFAULT 0,
  created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX ON games (status);
CREATE INDEX ON games (object_id);
```

---

## Status Lifecycle

```
GraphQL listing hit → INSERT (pending)
                           │
                    detail fetch OK → UPDATE (done)
                           │
                    detail fetch fail → stays (pending) → retried on next run
```

---

## Configuration (.env)

```
SUPABASE_URL=https://xxxx.supabase.co
SUPABASE_KEY=your-service-role-key
CONCURRENCY=5        # max parallel detail fetches
PAGE_DELAY=1.0       # seconds between GraphQL pages
```

---

## Dependencies

- `aiohttp` — async HTTP for detail page fetching
- `requests` — GraphQL listing API calls (synchronous, one page at a time)
- `supabase-py` — Supabase client
- `python-dotenv` — env var loading
- `beautifulsoup4` — HTML parsing fallback (primary: JSON extract from `__NEXT_DATA__`)
