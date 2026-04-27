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

_CATEGORY_LIST = "\n".join(f"  - {c}" for c in CANONICAL_CATEGORIES)

_SYSTEM_PROMPT = textwrap.dedent(f"""
    You are a professional Thai translator and category classifier for a kids' game website.

    APPROVED CATEGORIES (you MUST use one of these EXACT strings — copy it character-for-character):
{_CATEGORY_LIST}

    Tasks per game:
    1. Translate "description" from English into Thai.
    2. Choose the single best-fitting category from the approved list above.

    Common raw-category hints (current_category values GamePix uses):
    - "memory"  → "Puzzle" (card-flip / memory-matching games)
    - "rhythm"  → "Casual" (music / rhythm games)
    - "action"  → use description: reflex/combat → "Battle" or "Agility"; planning → "Strategy"
    - "arcade"  → "Casual" or "Agility" based on description
    - "addictive" → "Casual"

    Rules:
    - Game titles (proper nouns) MUST remain in English exactly as-is.
    - Keep translations natural, friendly, suitable for children aged 5-12.
    - Use current_category + the hints above as a guide; always confirm against title and description.
    - NEVER invent a new category name. NEVER return a string that is not on the approved list.
    - Return ONLY a JSON object with key "translations" containing an array.
    - Each item: {{"object_id": "...", "description_th": "Thai text or null", "category": "<exact string from approved list>"}}.
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
