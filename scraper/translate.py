"""
translate.py — Batch-translate game description & instruction into Thai using OpenAI GPT-4o-mini.

Usage:
    python translate.py              # translate all untranslated done records
    python translate.py --limit 50   # translate at most 50 records
    python translate.py --batch 10   # use batch size of 10 (default 10)

Rules:
  - Game titles that appear at the start of description are kept in English.
  - Output is saved to description_th and instruction_th columns in Supabase.
  - translation_status is updated to 'translated' or 'failed'.
"""

import argparse
import json
import textwrap
import time

import requests
from supabase import create_client, Client
from config import SUPABASE_URL, SUPABASE_KEY, OPENAI_API_KEY

# ─── OpenAI config ──────────────────────────────────────────────────────────
OPENAI_URL = "https://api.openai.com/v1/chat/completions"
DEFAULT_MODEL = "gpt-4o-mini"
DEFAULT_BATCH = 10

# ─── Supabase client ────────────────────────────────────────────────────────
_client: Client = create_client(SUPABASE_URL, SUPABASE_KEY)


# ─── DB helpers ─────────────────────────────────────────────────────────────

def load_untranslated(limit: int | None) -> list[dict]:
    """Load done records that have no translation_status yet (never attempted)."""
    q = (
        _client.table("games")
        .select("object_id, title, description, instruction")
        .eq("status", "done")
        .is_("translation_status", "null")
    )
    if limit:
        q = q.limit(limit)
    result = q.execute()
    return result.data


def save_translations(object_id: str, description_th: str | None, instruction_th: str | None) -> None:
    payload: dict = {"translation_status": "translated"}
    if description_th is not None:
        payload["description_th"] = description_th
    if instruction_th is not None:
        payload["instruction_th"] = instruction_th
    _client.table("games").update(payload).eq("object_id", object_id).execute()


def mark_translation_failed(object_id: str) -> None:
    """Mark a record as failed so it's skipped on future runs (unless reset)."""
    _client.table("games").update({"translation_status": "failed"}).eq("object_id", object_id).execute()


# ─── Prompt builder ─────────────────────────────────────────────────────────

SYSTEM_PROMPT = textwrap.dedent("""
    You are a professional Thai translator for a kids' game website.
    
    Rules you MUST follow:
    1. Translate the "description" and "instruction" fields from English into Thai.
    2. Game titles (proper nouns like "Skibidi Hero: Survivor IO") MUST remain in English exactly as-is — do NOT translate them.
    3. Other brand/product names should also remain in English.
    4. Keep the translation natural, friendly, and suitable for children aged 5–12.
    5. You will receive a JSON array of games. Return ONLY a valid JSON array with the same length, same order.
    6. Each item in the output must have exactly these fields:
       - "object_id": copy from input unchanged
       - "description_th": Thai translation of description (or null if description is null/empty)
       - "instruction_th": Thai translation of instruction (or null if instruction is null/empty)
    7. Do NOT include any explanation, markdown, or extra text — output raw JSON only.
    
    Example input:
    [{"object_id":"abc123","title":"Skibidi Hero","description":"Skibidi Hero is an action game where you survive waves of monsters.","instruction":"Use arrow keys to move."}]
    
    Example output:
    [{"object_id":"abc123","description_th":"Skibidi Hero เป็นเกมแอ็กชันที่คุณต้องเอาชีวิตรอดจากคลื่นมอนสเตอร์นับไม่ถ้วน","instruction_th":"ใช้ปุ่มลูกศรเพื่อเลื่อนตัวละคร"}]
""").strip()


def build_user_message(batch: list[dict]) -> str:
    items = [
        {
            "object_id": g["object_id"],
            "title": g.get("title") or "",
            "description": g.get("description") or "",
            "instruction": g.get("instruction") or "",
        }
        for g in batch
    ]
    return json.dumps(items, ensure_ascii=False)


# ─── OpenAI call ─────────────────────────────────────────────────────────────

def call_openai(model: str, user_message: str, retries: int = 3) -> str:
    if not OPENAI_API_KEY:
        raise ValueError("OPENAI_API_KEY is not set in your .env file")

    headers = {
        "Authorization": f"Bearer {OPENAI_API_KEY}",
        "Content-Type": "application/json"
    }
    payload = {
        "model": model,
        "messages": [
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": user_message},
        ],
        "response_format": {"type": "json_object"} if "mini" in model or "gpt-4o" in model else None,
        "temperature": 0.2,
    }
    
    # Adjust user message if using json_object mode to ensure root object is returned
    # OpenAI JSON mode requires the word 'json' in the prompt and returns an object.
    # Our prompt asks for an array. We'll wrap it in a root object to be safe.
    wrapped_user_msg = f"Return the translations in a JSON object with a key 'translations' containing the array:\n{user_message}"
    payload["messages"][1]["content"] = wrapped_user_msg

    for attempt in range(1, retries + 1):
        try:
            resp = requests.post(OPENAI_URL, json=payload, headers=headers, timeout=60)
            resp.raise_for_status()
            content = resp.json()["choices"][0]["message"]["content"]
            
            # Extract the array from the wrapped object
            data = json.loads(content)
            if "translations" in data:
                return json.dumps(data["translations"])
            return content
        except Exception as e:
            print(f"  [WARN] OpenAI attempt {attempt}/{retries} failed: {e}")
            if attempt < retries:
                time.sleep(2)
    raise RuntimeError("OpenAI failed after all retries")


# ─── Parse & validate response ───────────────────────────────────────────────

def parse_response(raw: str, expected_ids: list[str]) -> dict[str, dict]:
    """Parse JSON response and return a dict keyed by object_id."""
    try:
        data = json.loads(raw)
    except json.JSONDecodeError as e:
        raise ValueError(f"Invalid JSON from AI: {e}\nRaw:\n{raw[:500]}")

    if not isinstance(data, list):
        raise ValueError(f"Expected JSON array, got {type(data)}")

    result: dict[str, dict] = {}
    for item in data:
        oid = item.get("object_id")
        if oid and oid in expected_ids:
            result[oid] = {
                "description_th": item.get("description_th") or None,
                "instruction_th": item.get("instruction_th") or None,
            }
    return result


# ─── Main ────────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="Translate game descriptions to Thai using OpenAI")
    parser.add_argument("--limit", type=int, default=None, help="Max records to process (default: all)")
    parser.add_argument("--batch", type=int, default=DEFAULT_BATCH, help=f"Games per request (default: {DEFAULT_BATCH})")
    parser.add_argument("--model", type=str, default=DEFAULT_MODEL, help=f"Model to use (default: {DEFAULT_MODEL})")
    args = parser.parse_args()

    print(f"Model     : {args.model}")
    print(f"Batch size: {args.batch}")
    print(f"Limit     : {args.limit or 'all'}")
    print()

    records = load_untranslated(args.limit)
    total = len(records)
    print(f"Found {total} untranslated records.\n")

    if total == 0:
        print("Nothing to do.")
        return

    ok = 0
    failed = 0

    for i in range(0, total, args.batch):
        batch = records[i: i + args.batch]
        batch_num = i // args.batch + 1
        total_batches = (total + args.batch - 1) // args.batch
        ids = [g["object_id"] for g in batch]

        print(f"── Batch {batch_num}/{total_batches} ({len(batch)} games) ──")
        for g in batch:
            print(f"   {g['object_id']} | {g.get('title', '')[:50]}")

        try:
            user_msg = build_user_message(batch)
            raw = call_openai(args.model, user_msg)
            translations = parse_response(raw, ids)

            for g in batch:
                oid = g["object_id"]
                tr = translations.get(oid)
                if tr:
                    save_translations(oid, tr["description_th"], tr["instruction_th"])
                    print(f"   ✓ saved: {oid}")
                    ok += 1
                else:
                    print(f"   ✗ missing in response: {oid}")
                    mark_translation_failed(oid)
                    failed += 1

        except Exception as e:
            print(f"   [ERROR] Batch failed: {e}")
            for g in batch:
                mark_translation_failed(g["object_id"])
            failed += len(batch)

        print()

    print(f"Done. ✓ {ok} translated | ✗ {failed} failed")


if __name__ == "__main__":
    main()
