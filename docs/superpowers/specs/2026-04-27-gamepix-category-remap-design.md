# GamePix Category Remapping — Design Spec
_Date: 2026-04-27_

## Problem

GamePix sends raw category labels (e.g. `"action"`, `"sports"`) that do not match our canonical category list. Games currently ingested from GamePix have incorrect or non-matching `categories` in the DB. We need to:

1. Backfill existing GamePix games to use canonical categories.
2. Remap categories inline during future ingest so new games are stored correctly from the start.

---

## Canonical Category List

26 approved English categories (stored as a shared constant in both scripts):

```
Casual, Puzzle, Adventure, Racing & Driving, Simulation, Dress-up,
Agility, Shooter, Battle, Match-3, Strategy, Mahjong & Connect,
.IO, Art, Merge, Sports, Cards, Educational, Bubble Shooter,
Football, Cooking, Care, Boardgames, Basketball, Quiz, Jigsaw
```

OpenAI picks **exactly one** per game. The category stored in the DB is always from this list.

---

## Part 1 — New script: `gamepix_remap_categories.py`

### Purpose
Backfill existing GamePix games in Supabase with correctly mapped categories.

### Data flow
1. Query Supabase for all `provider = 'GamePix'` games: `object_id`, `title`, `description`, `categories`.
2. Optionally filter to only games whose current category is not already in the canonical list (`--only-unknown` flag).
3. Send batches to OpenAI. Each item: `object_id`, `title`, `description`, `current_category`.
4. OpenAI system prompt: pick exactly one canonical category based on current_category first; fall back to title + description if the raw label is ambiguous or unmapped.
5. Parse response: `{"mappings": [{"object_id": "...", "category": "..."}]}`.
6. Update `categories = [remapped_category]` in Supabase for each game.

### Flags
| Flag | Default | Purpose |
|---|---|---|
| `--batch N` | 10 | Games per OpenAI request |
| `--limit N` | all | Max games to process |
| `--dry-run` | off | Print mappings without writing to DB |
| `--only-unknown` | off | Only process games with non-canonical categories |

### OpenAI call
- Model: `gpt-4o-mini`
- Temperature: 0.2
- Response format: `json_object`
- Single focused call — no translation, no extra fields.

### Error handling
- Per-batch: if OpenAI fails, print error and skip the batch (no DB write). Continue to next batch.
- Per-game: if object_id missing from response, print warning and skip that game.

---

## Part 2 — Update `gamepix_main.py`

### What changes
- `_translate_batch()` is extended to also return a remapped category per game.
- System prompt gains a second task: pick one canonical category from the list.
- Each item in the OpenAI response gains a `"category"` field alongside `"description_th"`.
- After the combined call, `g["categories"] = [result["category"]]` is set before `post_batch()`.
- No additional OpenAI round-trips — category mapping is free-riding on the existing translation call.

### Updated response shape
```json
{
  "translations": [
    {
      "object_id": "gp_123",
      "description_th": "...",
      "category": "Shooter"
    }
  ]
}
```

### Fallback
If `category` is missing or not in the canonical list for a game, keep the original GamePix category unchanged (don't break ingest).

---

## Shared constant

Both files import from a new `categories.py` module in the scraper directory:

```python
CANONICAL_CATEGORIES = [
    "Casual", "Puzzle", "Adventure", "Racing & Driving", "Simulation",
    "Dress-up", "Agility", "Shooter", "Battle", "Match-3", "Strategy",
    "Mahjong & Connect", ".IO", "Art", "Merge", "Sports", "Cards",
    "Educational", "Bubble Shooter", "Football", "Cooking", "Care",
    "Boardgames", "Basketball", "Quiz", "Jigsaw",
]
```

---

## Files affected

| File | Change |
|---|---|
| `scraper/categories.py` | New — canonical category list constant |
| `scraper/gamepix_remap_categories.py` | New — backfill script |
| `scraper/gamepix_main.py` | Updated — extend `_translate_batch()` to remap category |

---

## Out of scope
- No changes to the webapp or DB schema — `categories` is already a text array column.
- No Thai translation of category names in these scripts (that's handled by the webapp's display layer).
- No changes to other providers (GameDistribute etc.).
