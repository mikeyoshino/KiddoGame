# scraper_service/main.py
import asyncio
import time
from pathlib import Path

from config import CONCURRENCY, PAGE_DELAY, THUMBNAIL_OUTPUT_DIR
from db import (
    load_existing_object_ids,
    load_pending_games,
    load_games_needing_thumbnails,
    load_games_with_local_thumbnails,
    mark_null_thumbnails_pending,
    reset_thumbnail_pending,
    insert_pending,
    update_done,
)
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


def mark_missing_local_thumbnails_pending() -> None:
    games = load_games_with_local_thumbnails()
    missing = [
        g for g in games
        if not (THUMBNAIL_OUTPUT_DIR / Path(g["thumbnail_url"]).name).exists()
    ]
    if not missing:
        print("All local thumbnails present on disk.")
        return
    print(f"Marking {len(missing)} records with missing local files as pending...")
    for game in missing:
        remote_url = f"https://img.gamedistribution.com/{Path(game['thumbnail_url']).name}"
        reset_thumbnail_pending(game["object_id"], remote_url)


async def repair_missing_thumbnails() -> None:
    games = load_games_needing_thumbnails()
    if not games:
        print("No missing thumbnails to repair.")
        return
    print(f"Repairing {len(games)} records with missing local thumbnails...")
    await download_thumbnails(games)


async def scrape_new_games(existing_ids: set[str]) -> None:
    print("Starting GraphQL listing scrape...")

    # Fetch page 1 just to get total_pages
    data = fetch_page(1)
    total_pages = get_total_pages(data)
    print(f"Total pages: {total_pages}")

    # Iterate in reverse so newest GD games (page 1) are inserted last,
    # giving them the most recent created_at and appearing first in the app.
    for page in range(total_pages, 0, -1):
        print(f"Page {page}/{total_pages}...")
        try:
            data = fetch_page(page)
            hits = parse_hits(data)
        except ValueError as e:
            print(f"  [WARN] Skipping page {page}: {e}")
            time.sleep(PAGE_DELAY)
            continue

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

            await download_thumbnails(new_games)

        skipped = len(hits) - len(new_games)
        print(f"  -> {len(new_games)} new, {skipped} skipped")
        time.sleep(PAGE_DELAY)

    print("Scrape complete.")


async def main() -> None:
    print("Loading existing records...")
    existing_ids = load_existing_object_ids()
    print(f"Found {len(existing_ids)} existing records.")

    mark_missing_local_thumbnails_pending()
    mark_null_thumbnails_pending()
    await retry_pending()
    await repair_missing_thumbnails()
    await scrape_new_games(existing_ids)


if __name__ == "__main__":
    asyncio.run(main())
