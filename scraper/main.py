import asyncio
import time

from config import CONCURRENCY, PAGE_DELAY
from gd_client import fetch_page, parse_hits, get_total_pages
from detail_fetcher import fetch_details_batch
from webapp_client import filter_new, post_batch

_BATCH_SIZE = 10


async def _send_batches(games: list[dict]) -> None:
    for i in range(0, len(games), _BATCH_SIZE):
        batch = games[i : i + _BATCH_SIZE]
        results = await post_batch(batch)
        for r in results:
            status = "OK" if r["ok"] else f"FAIL: {r.get('error', 'unknown')}"
            print(f"  [{status}] {r['object_id']}")


async def scrape_new_games() -> None:
    print("Starting GraphQL listing scrape...")
    data = fetch_page(1)
    total_pages = get_total_pages(data)
    print(f"Total pages: {total_pages}")

    for page in range(total_pages, 0, -1):
        print(f"Page {page}/{total_pages}...")
        try:
            data = fetch_page(page)
            hits = parse_hits(data)
        except ValueError as e:
            print(f"  [WARN] Skipping page {page}: {e}")
            time.sleep(PAGE_DELAY)
            continue

        object_ids = [g["object_id"] for g in hits]
        new_ids = set(await filter_new(object_ids))
        new_games = [g for g in hits if g["object_id"] in new_ids]

        if not new_games:
            print(f"  -> 0 new, {len(hits)} skipped")
            time.sleep(PAGE_DELAY)
            continue

        detail_results = await fetch_details_batch(new_games, CONCURRENCY)
        id_to_game = {g["object_id"]: g for g in new_games}
        full_games = [
            {**id_to_game[object_id], **detail}
            for object_id, detail in detail_results
            if detail and object_id in id_to_game
        ]

        skipped_detail = len(new_games) - len(full_games)
        print(
            f"  -> {len(full_games)} new, {len(hits) - len(new_games)} already known, "
            f"{skipped_detail} detail failed"
        )

        await _send_batches(full_games)
        time.sleep(PAGE_DELAY)

    print("Scrape complete.")


async def main() -> None:
    await scrape_new_games()


if __name__ == "__main__":
    asyncio.run(main())
