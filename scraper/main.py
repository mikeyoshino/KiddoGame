# scraper_service/main.py
import asyncio
import time

from config import CONCURRENCY, PAGE_DELAY
from db import load_existing_object_ids, load_pending_games, insert_pending, update_done
from gd_client import fetch_page, parse_hits, get_total_pages
from detail_fetcher import fetch_details_batch
from thumbnail_downloader import download_thumbnails


async def _process_detail_results(results: list[tuple[str, dict | None]]) -> None:
    for object_id, detail in results:
        if detail:
            update_done(object_id, detail)
            print(f"  [OK] done: {object_id}")
        else:
            print(f"  [PENDING] detail failed: {object_id}")


async def retry_pending() -> None:
    pending = load_pending_games()
    if not pending:
        print("No pending records.")
        return
    print(f"Retrying {len(pending)} pending records...")
    results = await fetch_details_batch(pending, CONCURRENCY)
    await _process_detail_results(results)

    # Download thumbnails for any pending records that were just recovered
    await download_thumbnails(pending)


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

            # Download thumbnails for this page's batch immediately
            # new_games already has thumbnail_url from parse_hits
            await download_thumbnails(new_games)

        skipped = len(hits) - len(new_games)
        print(f"  -> {len(new_games)} new, {skipped} skipped")
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
