# Scraper Service Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Python scraper service that pulls game data from gamedistribution.com and stores it in Supabase, resumable with a single command.

**Architecture:** Uses GraphQL API for paginated game listings, then fetches each game's HTML detail page to extract full metadata from `__NEXT_DATA__` JSON. Supabase stores all records with a `pending`/`done` status so partial failures are retried on next run.

**Tech Stack:** Python 3.11+, aiohttp (async detail fetching), requests (GraphQL listing), supabase-py, python-dotenv, pytest

---

## File Map

| File | Responsibility |
|------|---------------|
| `scraper_service/config.py` | Load and expose env vars |
| `scraper_service/db.py` | All Supabase reads/writes |
| `scraper_service/gd_client.py` | GraphQL listing API + response parsing |
| `scraper_service/detail_fetcher.py` | Async detail page fetch + `__NEXT_DATA__` parsing |
| `scraper_service/main.py` | Orchestration: retry pending, scrape new |
| `scraper_service/requirements.txt` | Dependencies |
| `scraper_service/.env.example` | Env var template |
| `scraper_service/sql/001_create_games.sql` | Supabase schema migration |
| `tests/test_gd_client.py` | Tests for listing parse logic |
| `tests/test_detail_fetcher.py` | Tests for `__NEXT_DATA__` parse logic |

---

## Task 1: Project Scaffold

**Files:**
- Create: `scraper_service/requirements.txt`
- Create: `scraper_service/.env.example`
- Create: `scraper_service/sql/001_create_games.sql`

- [ ] **Step 1: Create requirements.txt**

```
aiohttp==3.9.5
requests==2.31.0
supabase==2.4.6
python-dotenv==1.0.1
pytest==8.1.1
pytest-asyncio==0.23.6
```

- [ ] **Step 2: Create .env.example**

```
SUPABASE_URL=https://xxxx.supabase.co
SUPABASE_KEY=your-service-role-key
CONCURRENCY=5
PAGE_DELAY=1.0
```

- [ ] **Step 3: Create SQL migration**

```sql
-- scraper_service/sql/001_create_games.sql
CREATE TABLE IF NOT EXISTS games (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  object_id     TEXT UNIQUE NOT NULL,
  slug          TEXT UNIQUE NOT NULL,
  title         TEXT NOT NULL,
  company       TEXT,
  thumbnail_url TEXT,
  description   TEXT,
  instruction   TEXT,
  categories    TEXT[],
  tags          TEXT[],
  languages     TEXT[],
  gender        TEXT[],
  age_group     TEXT[],
  status        TEXT NOT NULL DEFAULT 'pending',
  view_count    INTEGER NOT NULL DEFAULT 0,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS games_status_idx ON games (status);
CREATE INDEX IF NOT EXISTS games_object_id_idx ON games (object_id);
```

- [ ] **Step 4: Run migration in Supabase**

Go to your Supabase project → SQL Editor → paste contents of `sql/001_create_games.sql` → Run.

- [ ] **Step 5: Install dependencies**

```bash
cd scraper_service
pip install -r requirements.txt
```

Expected: packages install without errors.

- [ ] **Step 6: Commit**

```bash
git add scraper_service/requirements.txt scraper_service/.env.example scraper_service/sql/001_create_games.sql
git commit -m "chore: scaffold scraper service project"
```

---

## Task 2: Config Module

**Files:**
- Create: `scraper_service/config.py`

- [ ] **Step 1: Create config.py**

```python
# scraper_service/config.py
import os
from dotenv import load_dotenv

load_dotenv()

SUPABASE_URL: str = os.environ["SUPABASE_URL"]
SUPABASE_KEY: str = os.environ["SUPABASE_KEY"]
CONCURRENCY: int = int(os.getenv("CONCURRENCY", "5"))
PAGE_DELAY: float = float(os.getenv("PAGE_DELAY", "1.0"))
```

- [ ] **Step 2: Copy .env.example to .env and fill in your values**

```bash
cp scraper_service/.env.example scraper_service/.env
# Edit .env with your actual Supabase URL and service-role key
```

- [ ] **Step 3: Verify config loads**

```bash
cd scraper_service
python -c "import config; print(config.SUPABASE_URL)"
```

Expected: prints your Supabase URL without error.

- [ ] **Step 4: Commit**

```bash
git add scraper_service/config.py
git commit -m "feat: add config module"
```

---

## Task 3: GraphQL Client

**Files:**
- Create: `scraper_service/gd_client.py`
- Create: `tests/test_gd_client.py`

- [ ] **Step 1: Write the failing tests**

```python
# tests/test_gd_client.py
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "scraper_service"))

from gd_client import parse_hits, get_total_pages

SAMPLE_RESPONSE = {
    "data": {
        "gamesSearched": {
            "hitsPerPage": 30,
            "nbHits": 21344,
            "nbPages": 712,
            "page": 1,
            "hits": [
                {
                    "objectID": "de35402342e2480f824b75e44f7ac5ba",
                    "title": "Hard Puzzle",
                    "company": "Playgama",
                    "visible": True,
                    "slugs": [{"name": "hard-puzzle", "__typename": "SlugType"}],
                    "assets": [
                        {"name": "de35402342e2480f824b75e44f7ac5ba-512x384.jpg", "__typename": "AssetType"}
                    ],
                    "__typename": "SearchHit"
                },
                {
                    "objectID": "invisible001",
                    "title": "Hidden Game",
                    "company": "Nobody",
                    "visible": False,
                    "slugs": [{"name": "hidden-game", "__typename": "SlugType"}],
                    "assets": [],
                    "__typename": "SearchHit"
                },
                {
                    "objectID": "noslug001",
                    "title": "No Slug Game",
                    "company": "Nobody",
                    "visible": True,
                    "slugs": [],
                    "assets": [],
                    "__typename": "SearchHit"
                }
            ]
        }
    }
}


def test_parse_hits_returns_visible_games_with_slugs():
    result = parse_hits(SAMPLE_RESPONSE)
    assert len(result) == 1
    assert result[0]["object_id"] == "de35402342e2480f824b75e44f7ac5ba"


def test_parse_hits_fields():
    result = parse_hits(SAMPLE_RESPONSE)
    game = result[0]
    assert game["slug"] == "hard-puzzle"
    assert game["title"] == "Hard Puzzle"
    assert game["company"] == "Playgama"
    assert game["thumbnail_url"] == "https://img.gamedistribution.com/de35402342e2480f824b75e44f7ac5ba-512x384.jpg"
    assert game["status"] == "pending"


def test_parse_hits_skips_invisible():
    result = parse_hits(SAMPLE_RESPONSE)
    object_ids = [g["object_id"] for g in result]
    assert "invisible001" not in object_ids


def test_parse_hits_skips_no_slug():
    result = parse_hits(SAMPLE_RESPONSE)
    object_ids = [g["object_id"] for g in result]
    assert "noslug001" not in object_ids


def test_get_total_pages():
    assert get_total_pages(SAMPLE_RESPONSE) == 712
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
cd scraper_service
pytest ../tests/test_gd_client.py -v
```

Expected: `ImportError` or `ModuleNotFoundError` — `gd_client` doesn't exist yet.

- [ ] **Step 3: Create gd_client.py**

```python
# scraper_service/gd_client.py
import requests

GQL_URL = "https://gd-website-api.gamedistribution.com/graphql"

_HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:150.0) Gecko/20100101 Firefox/150.0",
    "Accept": "*/*",
    "content-type": "application/json",
    "apollographql-client-name": "GDWebSite",
    "apollographql-client-version": "1.0",
    "authorization": "",
    "Origin": "https://gamedistribution.com",
    "Referer": "https://gamedistribution.com/",
}

_QUERY = (
    "fragment CoreGame on SearchHit {\n"
    "  objectID\n  title\n  company\n  visible\n"
    "  slugs { name __typename }\n"
    "  assets { name __typename }\n"
    "  __typename\n}\n\n"
    "query GetGamesSearched($id: String! = \"\", $perPage: Int! = 0, $page: Int! = 0, "
    "$search: String! = \"\", $UIfilter: UIFilterInput! = {}, $filters: GameSearchFiltersFlat! = {}) {\n"
    "  gamesSearched(input: {collectionObjectId: $id, hitsPerPage: $perPage, page: $page, "
    "search: $search, UIfilter: $UIfilter, filters: $filters}) {\n"
    "    hitsPerPage nbHits nbPages page\n"
    "    hits { ...CoreGame __typename }\n"
    "    __typename\n  }\n}"
)


def fetch_page(page: int, per_page: int = 30) -> dict:
    payload = {
        "operationName": "GetGamesSearched",
        "variables": {
            "id": "", "perPage": per_page, "page": page,
            "search": "", "UIfilter": {}, "filters": {}
        },
        "query": _QUERY,
    }
    response = requests.post(GQL_URL, json=payload, headers=_HEADERS, timeout=30)
    response.raise_for_status()
    return response.json()


def parse_hits(data: dict) -> list[dict]:
    hits = data["data"]["gamesSearched"]["hits"]
    games = []
    for hit in hits:
        if not hit.get("visible"):
            continue
        slugs = hit.get("slugs", [])
        if not slugs:
            continue
        slug = slugs[0]["name"]
        object_id = hit["objectID"]
        games.append({
            "object_id": object_id,
            "slug": slug,
            "title": hit["title"],
            "company": hit.get("company"),
            "thumbnail_url": f"https://img.gamedistribution.com/{object_id}-512x384.jpg",
            "status": "pending",
        })
    return games


def get_total_pages(data: dict) -> int:
    return data["data"]["gamesSearched"]["nbPages"]
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
pytest ../tests/test_gd_client.py -v
```

Expected: 5 tests pass.

- [ ] **Step 5: Smoke test the live API**

```bash
python -c "
from gd_client import fetch_page, parse_hits, get_total_pages
data = fetch_page(1)
print('Total pages:', get_total_pages(data))
hits = parse_hits(data)
print('First game:', hits[0])
"
```

Expected: prints total pages (~712) and a game dict with all fields populated.

- [ ] **Step 6: Commit**

```bash
git add scraper_service/gd_client.py tests/test_gd_client.py
git commit -m "feat: add GraphQL listing client"
```

---

## Task 4: Detail Fetcher

**Files:**
- Create: `scraper_service/detail_fetcher.py`
- Create: `tests/test_detail_fetcher.py`

- [ ] **Step 1: Write the failing tests**

```python
# tests/test_detail_fetcher.py
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "scraper_service"))

import json
from detail_fetcher import parse_next_data

def _make_html(game_props: dict) -> str:
    next_data = {
        "props": {
            "pageProps": {
                "game": game_props
            }
        }
    }
    json_str = json.dumps(next_data)
    return f'<html><head><script id="__NEXT_DATA__" type="application/json">{json_str}</script></head><body></body></html>'


def test_parse_next_data_returns_all_fields():
    html = _make_html({
        "description": "A great game",
        "instruction": "Click to play",
        "tags": ["clicker", "idle"],
        "categories": ["Casual"],
        "languages": ["English", "French"],
        "gender": ["Male", "Female"],
        "ageGroup": ["Kids", "Teens"],
    })
    result = parse_next_data(html)
    assert result is not None
    assert result["description"] == "A great game"
    assert result["instruction"] == "Click to play"
    assert result["tags"] == ["clicker", "idle"]
    assert result["categories"] == ["Casual"]
    assert result["languages"] == ["English", "French"]
    assert result["gender"] == ["Male", "Female"]
    assert result["age_group"] == ["Kids", "Teens"]


def test_parse_next_data_handles_missing_fields():
    html = _make_html({"description": "Only desc"})
    result = parse_next_data(html)
    assert result is not None
    assert result["description"] == "Only desc"
    assert result["instruction"] is None
    assert result["tags"] == []
    assert result["age_group"] == []


def test_parse_next_data_returns_none_when_no_script():
    result = parse_next_data("<html><body>No data here</body></html>")
    assert result is None


def test_parse_next_data_returns_none_on_bad_json():
    html = '<script id="__NEXT_DATA__" type="application/json">{bad json}</script>'
    result = parse_next_data(html)
    assert result is None


def test_parse_next_data_returns_none_when_game_key_missing():
    next_data = json.dumps({"props": {"pageProps": {}}})
    html = f'<script id="__NEXT_DATA__" type="application/json">{next_data}</script>'
    result = parse_next_data(html)
    assert result is None
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
pytest ../tests/test_detail_fetcher.py -v
```

Expected: `ImportError` — `detail_fetcher` doesn't exist yet.

- [ ] **Step 3: Create detail_fetcher.py**

```python
# scraper_service/detail_fetcher.py
import asyncio
import json
import re

import aiohttp

_DETAIL_URL = "https://gamedistribution.com/games/{slug}/"
_NEXT_DATA_RE = re.compile(
    r'<script id="__NEXT_DATA__" type="application/json">(.*?)</script>',
    re.DOTALL,
)
_HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:150.0) Gecko/20100101 Firefox/150.0",
}


def parse_next_data(html: str) -> dict | None:
    match = _NEXT_DATA_RE.search(html)
    if not match:
        return None
    try:
        data = json.loads(match.group(1))
        game = data["props"]["pageProps"]["game"]
    except (KeyError, json.JSONDecodeError):
        return None
    return {
        "description": game.get("description"),
        "instruction": game.get("instruction"),
        "categories": game.get("categories", []),
        "tags": game.get("tags", []),
        "languages": game.get("languages", []),
        "gender": game.get("gender", []),
        "age_group": game.get("ageGroup", []),
    }


async def _fetch_one(
    session: aiohttp.ClientSession,
    sem: asyncio.Semaphore,
    game: dict,
) -> tuple[str, dict | None]:
    async with sem:
        url = _DETAIL_URL.format(slug=game["slug"])
        try:
            async with session.get(
                url, headers=_HEADERS, timeout=aiohttp.ClientTimeout(total=30)
            ) as resp:
                if resp.status != 200:
                    return game["object_id"], None
                html = await resp.text()
                return game["object_id"], parse_next_data(html)
        except Exception:
            return game["object_id"], None


async def fetch_details_batch(
    games: list[dict], concurrency: int
) -> list[tuple[str, dict | None]]:
    """
    games: list of {"object_id": str, "slug": str}
    returns: list of (object_id, detail_dict or None)
    """
    sem = asyncio.Semaphore(concurrency)
    async with aiohttp.ClientSession() as session:
        tasks = [_fetch_one(session, sem, game) for game in games]
        return await asyncio.gather(*tasks)
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
pytest ../tests/test_detail_fetcher.py -v
```

Expected: 5 tests pass.

- [ ] **Step 5: Smoke test live detail fetch**

```bash
python -c "
import asyncio
from detail_fetcher import fetch_details_batch
games = [{'object_id': 'f078134f39634ca78dcd4a8479a314a2', 'slug': '67-clicker'}]
results = asyncio.run(fetch_details_batch(games, 1))
print(results[0])
"
```

Expected: `('f078134f39634ca78dcd4a8479a314a2', {'description': '...', 'instruction': '...', ...})`

- [ ] **Step 6: Commit**

```bash
git add scraper_service/detail_fetcher.py tests/test_detail_fetcher.py
git commit -m "feat: add detail page fetcher and __NEXT_DATA__ parser"
```

---

## Task 5: Database Module

**Files:**
- Create: `scraper_service/db.py`

- [ ] **Step 1: Create db.py**

```python
# scraper_service/db.py
from supabase import create_client, Client
from config import SUPABASE_URL, SUPABASE_KEY

_client: Client = create_client(SUPABASE_URL, SUPABASE_KEY)


def load_existing_object_ids() -> set[str]:
    """Load all object_ids (pending + done) for skip-checking."""
    result = _client.table("games").select("object_id").execute()
    return {row["object_id"] for row in result.data}


def load_pending_games() -> list[dict]:
    """Load all pending records to retry on startup."""
    result = (
        _client.table("games")
        .select("object_id,slug")
        .eq("status", "pending")
        .execute()
    )
    return result.data


def insert_pending(game: dict) -> None:
    """Insert a new game record with status=pending."""
    _client.table("games").insert(game).execute()


def update_done(object_id: str, detail: dict) -> None:
    """Update a game record with detail data and set status=done."""
    _client.table("games").update({**detail, "status": "done"}).eq(
        "object_id", object_id
    ).execute()
```

- [ ] **Step 2: Smoke test Supabase connection**

```bash
python -c "
from db import load_existing_object_ids
ids = load_existing_object_ids()
print(f'Existing records: {len(ids)}')
"
```

Expected: `Existing records: 0` (empty table on first run).

- [ ] **Step 3: Commit**

```bash
git add scraper_service/db.py
git commit -m "feat: add Supabase database module"
```

---

## Task 6: Main Orchestrator

**Files:**
- Create: `scraper_service/main.py`

- [ ] **Step 1: Create main.py**

```python
# scraper_service/main.py
import asyncio
import time

from config import CONCURRENCY, PAGE_DELAY
from db import load_existing_object_ids, load_pending_games, insert_pending, update_done
from gd_client import fetch_page, parse_hits, get_total_pages
from detail_fetcher import fetch_details_batch


async def _process_detail_results(results: list[tuple[str, dict | None]]) -> None:
    for object_id, detail in results:
        if detail:
            update_done(object_id, detail)
            print(f"  ✓ done: {object_id}")
        else:
            print(f"  ✗ pending (detail failed): {object_id}")


async def retry_pending() -> None:
    pending = load_pending_games()
    if not pending:
        print("No pending records.")
        return
    print(f"Retrying {len(pending)} pending records...")
    results = await fetch_details_batch(pending, CONCURRENCY)
    await _process_detail_results(results)


async def scrape_new_games(existing_ids: set[str]) -> None:
    print("Starting GraphQL listing scrape...")
    page = 1
    total_pages: int | None = None

    while total_pages is None or page <= total_pages:
        print(f"Page {page}/{total_pages or '?'}...")
        data = fetch_page(page)

        if total_pages is None:
            total_pages = get_total_pages(data)

        hits = parse_hits(data)
        new_games = [g for g in hits if g["object_id"] not in existing_ids]

        if new_games:
            for game in new_games:
                try:
                    insert_pending(game)
                    existing_ids.add(game["object_id"])
                except Exception as e:
                    print(f"  Insert error {game['object_id']}: {e}")

            results = await fetch_details_batch(new_games, CONCURRENCY)
            await _process_detail_results(results)

        skipped = len(hits) - len(new_games)
        print(f"  → {len(new_games)} new, {skipped} skipped")
        page += 1
        time.sleep(PAGE_DELAY)

    print("Scrape complete.")


async def main() -> None:
    print("Loading existing records...")
    existing_ids = load_existing_object_ids()
    print(f"Found {len(existing_ids)} existing records.")

    await retry_pending()
    await scrape_new_games(existing_ids)


if __name__ == "__main__":
    asyncio.run(main())
```

- [ ] **Step 2: Run a limited smoke test (2 pages)**

Temporarily set `PAGE_DELAY=0` and limit to 2 pages by adding `if page > 2: break` after `page += 1`, run, then revert.

```bash
python main.py
```

Expected output:
```
Loading existing records...
Found 0 existing records.
No pending records.
Starting GraphQL listing scrape...
Page 1/?...
  ✓ done: <objectID>
  ...
  → 30 new, 0 skipped
Page 2/712...
  ...
```

- [ ] **Step 3: Revert the smoke test limit and run full scrape**

Remove the `if page > 2: break` line.

```bash
python main.py
```

Expected: runs through all pages, inserting and updating records. Ctrl+C to stop early if needed — re-running resumes from where it left off.

- [ ] **Step 4: Verify resume works**

After stopping, re-run:

```bash
python main.py
```

Expected:
```
Loading existing records...
Found <N> existing records.
Retrying <M> pending records.   ← or "No pending records."
Starting GraphQL listing scrape...
Page 1/712...
  → 0 new, 30 skipped           ← already-scraped pages skip instantly
```

- [ ] **Step 5: Commit**

```bash
git add scraper_service/main.py
git commit -m "feat: add main orchestrator with resume and retry logic"
```

---

## Task 7: Run Full Test Suite

- [ ] **Step 1: Run all tests**

```bash
cd scraper_service
pytest ../tests/ -v
```

Expected: all tests pass with output like:
```
tests/test_gd_client.py::test_parse_hits_returns_visible_games_with_slugs PASSED
tests/test_gd_client.py::test_parse_hits_fields PASSED
tests/test_gd_client.py::test_parse_hits_skips_invisible PASSED
tests/test_gd_client.py::test_parse_hits_skips_no_slug PASSED
tests/test_gd_client.py::test_get_total_pages PASSED
tests/test_detail_fetcher.py::test_parse_next_data_returns_all_fields PASSED
tests/test_detail_fetcher.py::test_parse_next_data_handles_missing_fields PASSED
tests/test_detail_fetcher.py::test_parse_next_data_returns_none_when_no_script PASSED
tests/test_detail_fetcher.py::test_parse_next_data_returns_none_on_bad_json PASSED
tests/test_detail_fetcher.py::test_parse_next_data_returns_none_when_game_key_missing PASSED
10 passed in Xs
```

- [ ] **Step 2: Final commit**

```bash
git add -A
git commit -m "chore: all tests passing, scraper service complete"
```
