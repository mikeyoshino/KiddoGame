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

_CATEGORY_LIST = "\n".join(f"  - {c}" for c in CANONICAL_CATEGORIES)

_SYSTEM_PROMPT = textwrap.dedent(f"""
    You are a game category classifier for a kids' game website.

    APPROVED CATEGORIES (you MUST use one of these EXACT strings — copy it character-for-character):
{_CATEGORY_LIST}

    Rules:
    1. Choose the single best-fitting category from the approved list above.
    2. Use current_category as a hint. If it maps clearly to an approved category, prefer that.
    3. If current_category is not in the list (e.g. "Rhythm", "Action", "Arcade"), ignore it and use the title and description to decide.
    4. NEVER invent a new category name. NEVER return a string that is not on the approved list.
    5. Return ONLY a JSON object with key "mappings" containing an array.
    6. Each item: {{"object_id": "...", "category": "<exact string from approved list>"}}.
    7. Do NOT add explanation, markdown, or extra text.
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

    # Case-insensitive lookup so "shooter" or "Shooter" both resolve correctly
    canonical_lower = {c.lower(): c for c in CANONICAL_CATEGORIES}

    result = {}
    for item in data.get("mappings", []):
        oid = item.get("object_id")
        raw_cat = item.get("category") or ""
        canonical = canonical_lower.get(raw_cat.lower())
        if canonical:
            result[oid] = canonical
        else:
            print(f"   [WARN] OpenAI returned unknown category {raw_cat!r} for {oid}")
    return result


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
