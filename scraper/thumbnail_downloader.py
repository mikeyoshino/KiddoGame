"""
thumbnail_downloader.py
-----------------------
Downloads game thumbnail images from their remote URLs and saves them to the
Blazor webapp's static folder (wwwroot/images/games/).

After saving, the DB record's thumbnail_url is updated to the local web path
(e.g. /images/games/<object_id>.jpg) so the Blazor app can serve it directly
via UseStaticFiles().

Usage (standalone):
    python thumbnail_downloader.py            # download all pending thumbnails
    python thumbnail_downloader.py --dry-run  # print what would be downloaded
"""

import asyncio
import argparse
import sys
from pathlib import Path
from urllib.parse import urlparse

import aiohttp

from config import CONCURRENCY, THUMBNAIL_OUTPUT_DIR, THUMBNAIL_URL_PREFIX
from db import load_games_needing_thumbnails, update_thumbnail_url

_HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:150.0) "
        "Gecko/20100101 Firefox/150.0"
    )
}


def _local_filename(object_id: str, remote_url: str) -> str:
    """Derive a local filename: keep the original extension if present."""
    parsed = urlparse(remote_url)
    suffix = Path(parsed.path).suffix or ".jpg"
    return f"{object_id}{suffix}"


async def _download_one(
    session: aiohttp.ClientSession,
    sem: asyncio.Semaphore,
    game: dict,
    output_dir: Path,
    url_prefix: str,
    dry_run: bool,
) -> tuple[str, str | None]:
    """
    Download a single thumbnail.

    Returns (object_id, local_url) on success, (object_id, None) on failure.
    """
    object_id: str = game["object_id"]
    remote_url: str = game["thumbnail_url"]
    filename = _local_filename(object_id, remote_url)
    dest_path = output_dir / filename
    local_url = f"{url_prefix}/{filename}"

    # Skip if already downloaded
    if dest_path.exists():
        print(f"  [SKIP] exists: {filename}")
        return object_id, local_url

    if dry_run:
        print(f"  [DRY-RUN] would download: {remote_url} -> {dest_path}")
        return object_id, None

    async with sem:
        try:
            async with session.get(
                remote_url,
                headers=_HEADERS,
                timeout=aiohttp.ClientTimeout(total=30),
            ) as resp:
                if resp.status != 200:
                    print(f"  [FAIL] HTTP {resp.status}: {remote_url}")
                    return object_id, None
                data = await resp.read()

            dest_path.write_bytes(data)
            print(f"  [OK] saved: {filename} ({len(data):,} bytes)")
            return object_id, local_url

        except Exception as exc:
            print(f"  [ERR] {object_id}: {exc}")
            return object_id, None


async def download_thumbnails(
    games: list[dict],
    *,
    concurrency: int = CONCURRENCY,
    output_dir: Path = THUMBNAIL_OUTPUT_DIR,
    url_prefix: str = THUMBNAIL_URL_PREFIX,
    dry_run: bool = False,
    update_db: bool = True,
) -> None:
    """
    Download thumbnails for all supplied game records and update the DB.

    Parameters
    ----------
    games       : list of {object_id, thumbnail_url} dicts
    concurrency : max parallel HTTP requests
    output_dir  : filesystem folder to write images to
    url_prefix  : web path prefix for the saved images
    dry_run     : if True, only print actions without writing files / DB
    update_db   : if True, update thumbnail_url in the DB after download
    """
    if not games:
        print("No thumbnails to download.")
        return

    output_dir.mkdir(parents=True, exist_ok=True)
    print(f"Thumbnail output dir: {output_dir}")
    print(f"Processing {len(games)} thumbnails (concurrency={concurrency})...")

    sem = asyncio.Semaphore(concurrency)
    async with aiohttp.ClientSession() as session:
        tasks = [
            _download_one(session, sem, g, output_dir, url_prefix, dry_run)
            for g in games
        ]
        results = await asyncio.gather(*tasks)

    if not update_db or dry_run:
        return

    updated = 0
    for object_id, local_url in results:
        if local_url is not None:
            try:
                update_thumbnail_url(object_id, local_url)
                updated += 1
            except Exception as exc:
                print(f"  [DB ERR] {object_id}: {exc}")

    print(f"DB updated: {updated}/{len(games)} records.")


async def main_async(dry_run: bool) -> None:
    print("Loading games with remote thumbnails...")
    games = load_games_needing_thumbnails()
    print(f"Found {len(games)} game(s) needing thumbnail download.")
    await download_thumbnails(games, dry_run=dry_run)


def main() -> None:
    parser = argparse.ArgumentParser(description="Download game thumbnails to wwwroot.")
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print actions without downloading or updating the DB.",
    )
    args = parser.parse_args()
    asyncio.run(main_async(dry_run=args.dry_run))


if __name__ == "__main__":
    main()
