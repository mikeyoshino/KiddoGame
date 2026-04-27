# GamePix Category Remapping Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remap raw GamePix categories (e.g. `"action"`) to our canonical 26-category list — both for existing DB records (backfill script) and for new games during ingest (updated `gamepix_main.py`).

**Architecture:** A shared `categories.py` constant drives both a standalone backfill script (`gamepix_remap_categories.py`) and an extended OpenAI call in `gamepix_main.py` that combines Thai translation with category mapping in a single round-trip.

**Tech Stack:** Python 3.12, requests, supabase-py, pytest, OpenAI `gpt-4o-mini`

---

## File Map

| File | Action | Purpose |
|---|---|---|
| `scraper/categories.py` | **Create** | Shared `CANONICAL_CATEGORIES` list |
| `scraper/tests/test_categories.py` | **Create** | Sanity test for the list |
| `scraper/gamepix_remap_categories.py` | **Create** | Backfill script: query Supabase → OpenAI → update DB |
| `scraper/tests/test_gamepix_remap_categories.py` | **Create** | Tests for OpenAI parse + update logic |
| `scraper/gamepix_main.py` | **Modify** | Extend `_translate_batch` → `_translate_and_categorize_batch` |
| `scraper/tests/test_gamepix_main.py` | **Modify** | Update tests for new function signature |

---

### Task 1: Create `categories.py`

**Files:**
- Create: `scraper/categories.py`
- Create: `scraper/tests/test_categories.py`

- [ ] **Step 1: Write the failing test**

```python
# scraper/tests/test_categories.py
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from categories import CANONICAL_CATEGORIES


def test_canonical_categories_is_list():
    assert isinstance(CANONICAL_CATEGORIES, list)


def test_canonical_categories_count():
    assert len(CANONICAL_CATEGORIES) == 26


def test_canonical_categories_contains_expected():
    assert "Casual" in CANONICAL_CATEGORIES
    assert "Shooter" in CANONICAL_CATEGORIES
    assert "Racing & Driving" in CANONICAL_CATEGORIES
    assert ".IO" in CANONICAL_CATEGORIES
    assert "Jigsaw" in CANONICAL_CATEGORIES


def test_canonical_categories_no_duplicates():
    assert len(CANONICAL_CATEGORIES) == len(set(CANONICAL_CATEGORIES))
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd /Users/mikeyoshino/gitRepos/KiddoGame/scraper
source venv/bin/activate
pytest tests/test_categories.py -v
```

Expected: `ModuleNotFoundError: No module named 'categories'`

- [ ] **Step 3: Create `categories.py`**

```python
# scraper/categories.py
CANONICAL_CATEGORIES = [
    "Casual",
    "Puzzle",
    "Adventure",
    "Racing & Driving",
    "Simulation",
    "Dress-up",
    "Agility",
    "Shooter",
    "Battle",
    "Match-3",
    "Strategy",
    "Mahjong & Connect",
    ".IO",
    "Art",
    "Merge",
    "Sports",
    "Cards",
    "Educational",
    "Bubble Shooter",
    "Football",
    "Cooking",
    "Care",
    "Boardgames",
    "Basketball",
    "Quiz",
    "Jigsaw",
]
```

- [ ] **Step 4: Run test to verify it passes**

```bash
pytest tests/test_categories.py -v
```

Expected: 4 passed

- [ ] **Step 5: Commit**

```bash
git add scraper/categories.py scraper/tests/test_categories.py
git commit -m "feat: add canonical categories list"
```

---

### Task 2: Create `gamepix_remap_categories.py` — core logic

**Files:**
- Create: `scraper/gamepix_remap_categories.py`
- Create: `scraper/tests/test_gamepix_remap_categories.py`

- [ ] **Step 1: Write failing tests for `_remap_category_batch` and `load_gamepix_games`**

```python
# scraper/tests/test_gamepix_remap_categories.py
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import json
import pytest
from unittest.mock import patch, MagicMock
import gamepix_remap_categories as remap


# ── _remap_category_batch ─────────────────────────────────────────────────────

def test_remap_category_batch_raises_without_api_key():
    with patch.object(remap, "OPENAI_API_KEY", ""):
        with pytest.raises(ValueError, match="OPENAI_API_KEY"):
            remap._remap_category_batch([{"object_id": "gp_1", "title": "X", "description": "", "categories": ["action"]}])


def test_remap_category_batch_parses_valid_response():
    fake_response = {
        "choices": [{
            "message": {
                "content": json.dumps({
                    "mappings": [
                        {"object_id": "gp_1", "category": "Shooter"},
                        {"object_id": "gp_2", "category": "Puzzle"},
                    ]
                })
            }
        }]
    }
    mock_resp = MagicMock()
    mock_resp.raise_for_status = MagicMock()
    mock_resp.json.return_value = fake_response

    games = [
        {"object_id": "gp_1", "title": "Gun Game", "description": "Shoot things", "categories": ["action"]},
        {"object_id": "gp_2", "title": "Brain Game", "description": "Solve puzzles", "categories": ["puzzle"]},
    ]
    with patch.object(remap, "OPENAI_API_KEY", "sk-test"), \
         patch("gamepix_remap_categories.requests.post", return_value=mock_resp):
        result = remap._remap_category_batch(games)

    assert result == {"gp_1": "Shooter", "gp_2": "Puzzle"}


def test_remap_category_batch_ignores_non_canonical_response():
    fake_response = {
        "choices": [{
            "message": {
                "content": json.dumps({
                    "mappings": [
                        {"object_id": "gp_1", "category": "Action"},  # not in canonical list
                    ]
                })
            }
        }]
    }
    mock_resp = MagicMock()
    mock_resp.raise_for_status = MagicMock()
    mock_resp.json.return_value = fake_response

    games = [{"object_id": "gp_1", "title": "X", "description": "", "categories": ["action"]}]
    with patch.object(remap, "OPENAI_API_KEY", "sk-test"), \
         patch("gamepix_remap_categories.requests.post", return_value=mock_resp):
        result = remap._remap_category_batch(games)

    assert result == {}


# ── load_gamepix_games ────────────────────────────────────────────────────────

def test_load_gamepix_games_filters_only_unknown():
    all_games = [
        {"object_id": "gp_1", "title": "A", "description": "", "categories": ["action"]},
        {"object_id": "gp_2", "title": "B", "description": "", "categories": ["Shooter"]},  # already canonical
        {"object_id": "gp_3", "title": "C", "description": "", "categories": []},
    ]
    mock_result = MagicMock()
    mock_result.data = all_games

    mock_query = MagicMock()
    mock_query.eq.return_value = mock_query
    mock_query.range.return_value = mock_query
    mock_query.execute.return_value = mock_result

    mock_table = MagicMock()
    mock_table.select.return_value = mock_query

    with patch.object(remap._client, "table", return_value=mock_table):
        result = remap.load_gamepix_games(only_unknown=True, limit=None)

    # gp_2 has canonical "Shooter" — excluded
    assert [r["object_id"] for r in result] == ["gp_1", "gp_3"]


def test_load_gamepix_games_respects_limit():
    all_games = [
        {"object_id": f"gp_{i}", "title": f"Game {i}", "description": "", "categories": ["action"]}
        for i in range(20)
    ]
    mock_result = MagicMock()
    mock_result.data = all_games

    mock_query = MagicMock()
    mock_query.eq.return_value = mock_query
    mock_query.range.return_value = mock_query
    mock_query.execute.return_value = mock_result

    mock_table = MagicMock()
    mock_table.select.return_value = mock_query

    with patch.object(remap._client, "table", return_value=mock_table):
        result = remap.load_gamepix_games(only_unknown=False, limit=5)

    assert len(result) == 5


# ── _update_category ──────────────────────────────────────────────────────────

def test_update_category_calls_supabase():
    mock_update = MagicMock()
    mock_eq = MagicMock()
    mock_eq.execute = MagicMock()
    mock_update.eq.return_value = mock_eq

    mock_table = MagicMock()
    mock_table.update.return_value = mock_update

    with patch.object(remap._client, "table", return_value=mock_table):
        remap._update_category("gp_1", "Shooter")

    mock_table.update.assert_called_once_with({"categories": ["Shooter"]})
    mock_update.eq.assert_called_once_with("object_id", "gp_1")
    mock_eq.execute.assert_called_once()
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd /Users/mikeyoshino/gitRepos/KiddoGame/scraper
source venv/bin/activate
pytest tests/test_gamepix_remap_categories.py -v
```

Expected: `ModuleNotFoundError: No module named 'gamepix_remap_categories'`

- [ ] **Step 3: Create `gamepix_remap_categories.py`**

```python
# scraper/gamepix_remap_categories.py
"""
gamepix_remap_categories.py — Backfill canonical categories for existing GamePix games.

Usage:
    python gamepix_remap_categories.py
    python gamepix_remap_categories.py --only-unknown
    python gamepix_remap_categories.py --limit 100
    python gamepix_remap_categories.py --dry-run
"""

import argparse
import json
import textwrap

import requests
from supabase import create_client, Client
from config import SUPABASE_URL, SUPABASE_KEY, OPENAI_API_KEY
from categories import CANONICAL_CATEGORIES

_OPENAI_URL = "https://api.openai.com/v1/chat/completions"
_OPENAI_MODEL = "gpt-4o-mini"
_PAGE = 1000

_client: Client = create_client(SUPABASE_URL, SUPABASE_KEY)

_SYSTEM_PROMPT = textwrap.dedent(f"""
    You are a game category classifier for a kids' game website.
    Rules:
    1. Map each game to exactly one category from this approved list: {', '.join(CANONICAL_CATEGORIES)}
    2. Use current_category first if it clearly matches an approved category (case-insensitive).
    3. If current_category is ambiguous or not in the list, use title and description to decide.
    4. Return ONLY a JSON object with key "mappings" containing an array.
    5. Each item: {{"object_id": "...", "category": "one from the approved list"}}.
    6. Do NOT add explanation, markdown, or extra text.
""").strip()


def load_gamepix_games(only_unknown: bool, limit: int | None) -> list[dict]:
    rows: list[dict] = []
    offset = 0
    while True:
        result = (
            _client.table("games")
            .select("object_id,title,description,categories")
            .eq("provider", "GamePix")
            .eq("status", "done")
            .range(offset, offset + _PAGE - 1)
            .execute()
        )
        rows.extend(result.data)
        if len(result.data) < _PAGE:
            break
        offset += _PAGE

    if only_unknown:
        rows = [
            r for r in rows
            if not r.get("categories") or r["categories"][0] not in CANONICAL_CATEGORIES
        ]

    if limit:
        rows = rows[:limit]
    return rows


def _remap_category_batch(games: list[dict]) -> dict[str, str]:
    """Call OpenAI to map raw categories to canonical ones. Returns {object_id: category}."""
    if not OPENAI_API_KEY:
        raise ValueError("OPENAI_API_KEY not set in .env")

    items = [
        {
            "object_id": g["object_id"],
            "title": g.get("title") or "",
            "description": g.get("description") or "",
            "current_category": (g.get("categories") or [""])[0],
        }
        for g in games
    ]
    user_msg = (
        "Return mappings in a JSON object with key 'mappings' containing the array:\n"
        + json.dumps(items, ensure_ascii=False)
    )
    payload = {
        "model": _OPENAI_MODEL,
        "messages": [
            {"role": "system", "content": _SYSTEM_PROMPT},
            {"role": "user", "content": user_msg},
        ],
        "response_format": {"type": "json_object"},
        "temperature": 0.2,
    }
    headers = {
        "Authorization": f"Bearer {OPENAI_API_KEY}",
        "Content-Type": "application/json",
    }
    resp = requests.post(_OPENAI_URL, json=payload, headers=headers, timeout=60)
    resp.raise_for_status()
    content = resp.json()["choices"][0]["message"]["content"]
    data = json.loads(content)
    return {
        item["object_id"]: item["category"]
        for item in data.get("mappings", [])
        if item.get("category") in CANONICAL_CATEGORIES
    }


def _update_category(object_id: str, category: str) -> None:
    _client.table("games").update({"categories": [category]}).eq("object_id", object_id).execute()


def main() -> None:
    parser = argparse.ArgumentParser(description="Remap GamePix categories to canonical list")
    parser.add_argument("--batch", type=int, default=10, help="Games per OpenAI request (default: 10)")
    parser.add_argument("--limit", type=int, default=None, help="Max games to process (default: all)")
    parser.add_argument("--dry-run", action="store_true", help="Print mappings without writing to DB")
    parser.add_argument("--only-unknown", action="store_true", help="Only process games with non-canonical categories")
    args = parser.parse_args()

    print(f"Batch size  : {args.batch}")
    print(f"Limit       : {args.limit or 'all'}")
    print(f"Dry run     : {args.dry_run}")
    print(f"Only unknown: {args.only_unknown}")
    print()

    games = load_gamepix_games(args.only_unknown, args.limit)
    total = len(games)
    print(f"Found {total} GamePix games to remap.\n")

    if total == 0:
        print("Nothing to do.")
        return

    ok = failed = 0
    for i in range(0, total, args.batch):
        batch = games[i: i + args.batch]
        batch_num = i // args.batch + 1
        total_batches = (total + args.batch - 1) // args.batch
        print(f"── Batch {batch_num}/{total_batches} ({len(batch)} games) ──")
        try:
            mappings = _remap_category_batch(batch)
            for g in batch:
                oid = g["object_id"]
                cat = mappings.get(oid)
                if cat:
                    old = (g.get("categories") or ["?"])[0]
                    print(f"   {oid}: {old!r} → {cat!r}")
                    if not args.dry_run:
                        _update_category(oid, cat)
                    ok += 1
                else:
                    print(f"   {oid}: [no mapping returned]")
                    failed += 1
        except Exception as e:
            print(f"   [ERROR] Batch failed: {e}")
            failed += len(batch)
        print()

    suffix = " (dry run)" if args.dry_run else ""
    print(f"Done. ✓ {ok} remapped | ✗ {failed} failed{suffix}")


if __name__ == "__main__":
    main()
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
pytest tests/test_gamepix_remap_categories.py -v
```

Expected: 6 passed

- [ ] **Step 5: Commit**

```bash
git add scraper/gamepix_remap_categories.py scraper/tests/test_gamepix_remap_categories.py
git commit -m "feat: add gamepix_remap_categories backfill script"
```

---

### Task 3: Update `gamepix_main.py` — combined translate + categorize

**Files:**
- Modify: `scraper/gamepix_main.py`
- Modify: `scraper/tests/test_gamepix_main.py`

- [ ] **Step 1: Write failing tests for the updated function**

Add these tests to `scraper/tests/test_gamepix_main.py` (append to existing file):

```python
# ── _translate_and_categorize_batch ──────────────────────────────────────────

def test_translate_and_categorize_batch_raises_without_api_key():
    with patch.object(gamepix_main, "OPENAI_API_KEY", ""):
        with pytest.raises(ValueError, match="OPENAI_API_KEY"):
            gamepix_main._translate_and_categorize_batch([
                {"object_id": "gp_1", "title": "X", "description": "Y", "categories": ["action"]}
            ])


def test_translate_and_categorize_batch_returns_description_th_and_category():
    fake_response = {
        "choices": [{
            "message": {
                "content": json.dumps({
                    "translations": [
                        {"object_id": "gp_1", "description_th": "เกมสนุก", "category": "Shooter"}
                    ]
                })
            }
        }]
    }
    mock_resp = MagicMock()
    mock_resp.raise_for_status = MagicMock()
    mock_resp.json.return_value = fake_response

    with patch.object(gamepix_main, "OPENAI_API_KEY", "sk-test"), \
         patch("gamepix_main.requests.post", return_value=mock_resp):
        result = gamepix_main._translate_and_categorize_batch([
            {"object_id": "gp_1", "title": "Gun Game", "description": "Shoot things", "categories": ["action"]}
        ])

    assert result == {"gp_1": {"description_th": "เกมสนุก", "category": "Shooter"}}


def test_translate_and_categorize_batch_handles_missing_entry():
    fake_response = {
        "choices": [{"message": {"content": json.dumps({"translations": []})}}]
    }
    mock_resp = MagicMock()
    mock_resp.raise_for_status = MagicMock()
    mock_resp.json.return_value = fake_response

    with patch.object(gamepix_main, "OPENAI_API_KEY", "sk-test"), \
         patch("gamepix_main.requests.post", return_value=mock_resp):
        result = gamepix_main._translate_and_categorize_batch([
            {"object_id": "gp_1", "title": "X", "description": "Y", "categories": ["action"]}
        ])

    assert result == {}
```

- [ ] **Step 2: Run new tests to verify they fail**

```bash
cd /Users/mikeyoshino/gitRepos/KiddoGame/scraper
source venv/bin/activate
pytest tests/test_gamepix_main.py::test_translate_and_categorize_batch_raises_without_api_key \
       tests/test_gamepix_main.py::test_translate_and_categorize_batch_returns_description_th_and_category \
       tests/test_gamepix_main.py::test_translate_and_categorize_batch_handles_missing_entry -v
```

Expected: `AttributeError: module 'gamepix_main' has no attribute '_translate_and_categorize_batch'`

- [ ] **Step 3: Update `gamepix_main.py`**

Replace the file with the following (full content):

```python
import asyncio
import json
import sys
import textwrap
import time
from pathlib import Path

import requests

from categories import CANONICAL_CATEGORIES
from config import OPENAI_API_KEY, PAGE_DELAY
from gamepix_client import fetch_page, parse_items, has_next_page
from webapp_client import filter_new, post_batch, check_title_duplicates
from db import check_slug_duplicates

PROGRESS_FILE = Path(__file__).parent / "gamepix_progress.json"
_BATCH_SIZE = 10
_OPENAI_URL = "https://api.openai.com/v1/chat/completions"
_OPENAI_MODEL = "gpt-4o-mini"

_SYSTEM_PROMPT = textwrap.dedent(f"""
    You are a professional Thai translator and category classifier for a kids' game website.
    Tasks:
    1. Translate "description" from English into Thai.
    2. Map each game to exactly one category from this approved list: {', '.join(CANONICAL_CATEGORIES)}
    Rules:
    - Game titles (proper nouns) MUST remain in English exactly as-is.
    - Keep translations natural, friendly, suitable for children aged 5-12.
    - Use current_category if it clearly matches an approved category; otherwise use title and description.
    - Return ONLY a JSON object with key "translations" containing an array.
    - Each item: {{"object_id": "...", "description_th": "Thai text or null", "category": "one from the approved list"}}.
    - Do NOT add explanation, markdown, or extra text.
""").strip()


def _load_progress() -> int:
    if PROGRESS_FILE.exists():
        data = json.loads(PROGRESS_FILE.read_text())
        return data.get("last_completed_page", 0)
    return 0


def _save_progress(page: int) -> None:
    PROGRESS_FILE.write_text(json.dumps({"last_completed_page": page}))


def _delete_progress() -> None:
    PROGRESS_FILE.unlink(missing_ok=True)


def _translate_and_categorize_batch(games: list[dict]) -> dict[str, dict]:
    """Translate descriptions and remap categories in one OpenAI call.

    Returns {object_id: {"description_th": str|None, "category": str|None}}.
    Raises on failure.
    """
    if not OPENAI_API_KEY:
        raise ValueError("OPENAI_API_KEY not set in .env")

    items = [
        {
            "object_id": g["object_id"],
            "title": g["title"],
            "description": g.get("description") or "",
            "current_category": (g.get("categories") or [""])[0],
        }
        for g in games
    ]
    user_msg = (
        "Return translations in a JSON object with key 'translations' containing the array:\n"
        + json.dumps(items, ensure_ascii=False)
    )
    payload = {
        "model": _OPENAI_MODEL,
        "messages": [
            {"role": "system", "content": _SYSTEM_PROMPT},
            {"role": "user", "content": user_msg},
        ],
        "response_format": {"type": "json_object"},
        "temperature": 0.2,
    }
    headers = {
        "Authorization": f"Bearer {OPENAI_API_KEY}",
        "Content-Type": "application/json",
    }

    resp = requests.post(_OPENAI_URL, json=payload, headers=headers, timeout=60)
    resp.raise_for_status()

    content = resp.json()["choices"][0]["message"]["content"]
    data = json.loads(content)
    return {
        item["object_id"]: {
            "description_th": item.get("description_th"),
            "category": item.get("category"),
        }
        for item in data.get("translations", [])
    }


async def _process_page(games: list[dict]) -> bool:
    """Filter, translate, categorize, and post one page of games. Returns False if script should stop."""
    object_ids = [g["object_id"] for g in games]
    new_ids = set(await filter_new(object_ids))
    new_games = [g for g in games if g["object_id"] in new_ids]

    if not new_games:
        print(f"  -> 0 new, {len(games)} already known")
        return True

    titles = [g["title"] for g in new_games]
    duplicate_titles = set(await check_title_duplicates(titles))
    unique_games_by_title = [g for g in new_games if g["title"] not in duplicate_titles]

    skipped_titles = len(new_games) - len(unique_games_by_title)
    if skipped_titles:
        print(f"  -> {skipped_titles} skipped (title exists in DB from another provider)")

    slugs = [g["slug"] for g in unique_games_by_title]
    duplicate_slugs = set(check_slug_duplicates(slugs))
    unique_games = [g for g in unique_games_by_title if g["slug"] not in duplicate_slugs]

    skipped_slugs = len(unique_games_by_title) - len(unique_games)
    if skipped_slugs:
        print(f"  -> {skipped_slugs} skipped (slug exists in DB from another provider)")

    if not unique_games:
        return True

    translation_failed = False
    translation_map: dict[str, dict] = {}

    for i in range(0, len(unique_games), _BATCH_SIZE):
        batch = unique_games[i: i + _BATCH_SIZE]
        batch_num = i // _BATCH_SIZE + 1
        total_batches = (len(unique_games) + _BATCH_SIZE - 1) // _BATCH_SIZE
        print(f"  Translating batch {batch_num}/{total_batches} ({len(batch)} games)...", flush=True)
        try:
            result = _translate_and_categorize_batch(batch)
            translation_map.update(result)
        except Exception as e:
            print(f"  [ERROR] Translation failed: {e}")
            translation_failed = True
            break

    for g in unique_games:
        entry = translation_map.get(g["object_id"]) if not translation_failed else None
        g["description_th"] = entry["description_th"] if entry else None
        g["instruction_th"] = None
        g["translation_status"] = "translated" if g["object_id"] in translation_map else None
        if entry and entry.get("category") in CANONICAL_CATEGORIES:
            g["categories"] = [entry["category"]]

    for i in range(0, len(unique_games), _BATCH_SIZE):
        batch = unique_games[i: i + _BATCH_SIZE]
        batch_num = i // _BATCH_SIZE + 1
        total_batches = (len(unique_games) + _BATCH_SIZE - 1) // _BATCH_SIZE
        print(f"  Posting batch {batch_num}/{total_batches} ({len(batch)} games, downloading thumbnails)...", flush=True)
        results = await post_batch(batch)
        for r in results:
            status = "OK" if r["ok"] else f"FAIL: {r.get('error', 'unknown')}"
            print(f"  [{status}] {r['object_id']}")

    print(f"  -> {len(unique_games)} ingested ({len(translation_map)} translated)")
    return not translation_failed


async def main() -> None:
    start_page = _load_progress() + 1
    print(f"Starting GamePix scrape from page {start_page}...")

    page = start_page
    while True:
        print(f"Page {page}...")
        try:
            data = fetch_page(page)
        except Exception as e:
            print(f"  [ERROR] Failed to fetch page {page}: {e}")
            break

        games = parse_items(data)
        if not games:
            print("  -> empty page, done")
            break

        ok = await _process_page(games)
        _save_progress(page)

        if not ok:
            print(
                f"\nTranslation failed. Progress saved at page {page}.\n"
                "Re-run this script to resume from here.\n"
                "Or run 'python translate.py' to translate games with null translation_status."
            )
            sys.exit(1)

        if not has_next_page(data):
            print("No more pages.")
            break

        page += 1
        time.sleep(PAGE_DELAY)

    print("Scrape complete.")
    _delete_progress()


if __name__ == "__main__":
    asyncio.run(main())
```

- [ ] **Step 4: Update the old `_translate_batch` tests in `test_gamepix_main.py`**

The three old tests reference `_translate_batch` which no longer exists. Replace them with updated versions.
Find and replace the three tests that reference `_translate_batch` with:

```python
# ── _translate_and_categorize_batch (replaces _translate_batch) ──────────────

def test_translate_and_categorize_batch_raises_without_api_key():
    with patch.object(gamepix_main, "OPENAI_API_KEY", ""):
        with pytest.raises(ValueError, match="OPENAI_API_KEY"):
            gamepix_main._translate_and_categorize_batch([
                {"object_id": "gp_1", "title": "X", "description": "Y", "categories": ["action"]}
            ])


def test_translate_and_categorize_batch_returns_description_th_and_category():
    fake_response = {
        "choices": [{
            "message": {
                "content": json.dumps({
                    "translations": [
                        {"object_id": "gp_1", "description_th": "เกมสนุก", "category": "Shooter"}
                    ]
                })
            }
        }]
    }
    mock_resp = MagicMock()
    mock_resp.raise_for_status = MagicMock()
    mock_resp.json.return_value = fake_response

    with patch.object(gamepix_main, "OPENAI_API_KEY", "sk-test"), \
         patch("gamepix_main.requests.post", return_value=mock_resp):
        result = gamepix_main._translate_and_categorize_batch([
            {"object_id": "gp_1", "title": "Gun Game", "description": "Shoot things", "categories": ["action"]}
        ])

    assert result == {"gp_1": {"description_th": "เกมสนุก", "category": "Shooter"}}


def test_translate_and_categorize_batch_handles_missing_entry():
    fake_response = {
        "choices": [{"message": {"content": json.dumps({"translations": []})}}]
    }
    mock_resp = MagicMock()
    mock_resp.raise_for_status = MagicMock()
    mock_resp.json.return_value = fake_response

    with patch.object(gamepix_main, "OPENAI_API_KEY", "sk-test"), \
         patch("gamepix_main.requests.post", return_value=mock_resp):
        result = gamepix_main._translate_and_categorize_batch([
            {"object_id": "gp_1", "title": "X", "description": "Y", "categories": ["action"]}
        ])

    assert result == {}
```

> Note: The full replacement content for `test_gamepix_main.py` that keeps the progress tests and replaces the translation tests:

```python
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import json
import pytest
from pathlib import Path
from unittest.mock import patch, MagicMock
import gamepix_main


# ── Progress helpers ──────────────────────────────────────────────────────────

def test_load_progress_returns_zero_when_no_file(tmp_path):
    with patch.object(gamepix_main, "PROGRESS_FILE", tmp_path / "progress.json"):
        assert gamepix_main._load_progress() == 0


def test_save_and_load_progress(tmp_path):
    pf = tmp_path / "progress.json"
    with patch.object(gamepix_main, "PROGRESS_FILE", pf):
        gamepix_main._save_progress(7)
        assert gamepix_main._load_progress() == 7


def test_delete_progress_removes_file(tmp_path):
    pf = tmp_path / "progress.json"
    pf.write_text('{"last_completed_page": 3}')
    with patch.object(gamepix_main, "PROGRESS_FILE", pf):
        gamepix_main._delete_progress()
        assert not pf.exists()


def test_delete_progress_is_safe_when_no_file(tmp_path):
    with patch.object(gamepix_main, "PROGRESS_FILE", tmp_path / "missing.json"):
        gamepix_main._delete_progress()  # should not raise


# ── _translate_and_categorize_batch ──────────────────────────────────────────

def test_translate_and_categorize_batch_raises_without_api_key():
    with patch.object(gamepix_main, "OPENAI_API_KEY", ""):
        with pytest.raises(ValueError, match="OPENAI_API_KEY"):
            gamepix_main._translate_and_categorize_batch([
                {"object_id": "gp_1", "title": "X", "description": "Y", "categories": ["action"]}
            ])


def test_translate_and_categorize_batch_returns_description_th_and_category():
    fake_response = {
        "choices": [{
            "message": {
                "content": json.dumps({
                    "translations": [
                        {"object_id": "gp_1", "description_th": "เกมสนุก", "category": "Shooter"}
                    ]
                })
            }
        }]
    }
    mock_resp = MagicMock()
    mock_resp.raise_for_status = MagicMock()
    mock_resp.json.return_value = fake_response

    with patch.object(gamepix_main, "OPENAI_API_KEY", "sk-test"), \
         patch("gamepix_main.requests.post", return_value=mock_resp):
        result = gamepix_main._translate_and_categorize_batch([
            {"object_id": "gp_1", "title": "Gun Game", "description": "Shoot things", "categories": ["action"]}
        ])

    assert result == {"gp_1": {"description_th": "เกมสนุก", "category": "Shooter"}}


def test_translate_and_categorize_batch_handles_missing_entry():
    fake_response = {
        "choices": [{"message": {"content": json.dumps({"translations": []})}}]
    }
    mock_resp = MagicMock()
    mock_resp.raise_for_status = MagicMock()
    mock_resp.json.return_value = fake_response

    with patch.object(gamepix_main, "OPENAI_API_KEY", "sk-test"), \
         patch("gamepix_main.requests.post", return_value=mock_resp):
        result = gamepix_main._translate_and_categorize_batch([
            {"object_id": "gp_1", "title": "X", "description": "Y", "categories": ["action"]}
        ])

    assert result == {}
```

- [ ] **Step 5: Run all scraper tests**

```bash
cd /Users/mikeyoshino/gitRepos/KiddoGame/scraper
source venv/bin/activate
pytest tests/ -v
```

Expected: all tests pass (the 3 old `_translate_batch` tests replaced, 3 new ones pass)

- [ ] **Step 6: Commit**

```bash
git add scraper/gamepix_main.py scraper/tests/test_gamepix_main.py
git commit -m "feat: combine category remapping with translation in gamepix_main"
```

---

## Self-Review

**Spec coverage:**
- ✅ `categories.py` shared constant — Task 1
- ✅ Backfill script with `--batch`, `--limit`, `--dry-run`, `--only-unknown` — Task 2
- ✅ OpenAI prompt picks canonical category; ignores non-canonical responses — Task 2
- ✅ `gamepix_main.py` combined call returns `description_th` + `category` — Task 3
- ✅ Fallback: if category not in canonical list, original `categories` field unchanged — Task 3 (`_process_page` only updates if `entry.get("category") in CANONICAL_CATEGORIES`)
- ✅ No webapp/schema changes — confirmed, only `categories` column updated (already text array)

**Placeholder scan:** None found. All steps have complete code.

**Type consistency:**
- `_translate_and_categorize_batch` returns `dict[str, dict]` throughout Tasks 2 and 3 ✅
- `_remap_category_batch` returns `dict[str, str]` throughout Task 2 ✅
- `load_gamepix_games` signature `(only_unknown: bool, limit: int | None)` consistent between implementation and tests ✅
