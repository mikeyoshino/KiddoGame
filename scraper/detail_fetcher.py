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
