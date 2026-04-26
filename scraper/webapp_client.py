import aiohttp
from config import WEBAPP_URL

_FILTER_TIMEOUT = aiohttp.ClientTimeout(total=30)
_BATCH_TIMEOUT = aiohttp.ClientTimeout(total=120)


async def filter_new(object_ids: list[str]) -> list[str]:
    """Return object_ids that are new or pending (not yet done in the webapp DB)."""
    if not object_ids:
        return []
    async with aiohttp.ClientSession() as session:
        async with session.post(
            f"{WEBAPP_URL}/api/ingest/filter-new",
            json=object_ids,
            timeout=_FILTER_TIMEOUT,
        ) as resp:
            resp.raise_for_status()
            return await resp.json()


async def post_batch(games: list[dict]) -> list[dict]:
    """Send a batch of up to 10 games to the webapp ingest endpoint.

    Returns per-game results: [{object_id, ok, error}, ...]
    """
    async with aiohttp.ClientSession() as session:
        async with session.post(
            f"{WEBAPP_URL}/api/ingest/batch",
            json={"games": games},
            timeout=_BATCH_TIMEOUT,
        ) as resp:
            resp.raise_for_status()
            data = await resp.json()
            return data["results"]
