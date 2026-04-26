import requests

_FEED_BASE = "https://feeds.gamepix.com/v2/json"
_SID = "22322"
_PAGE_SIZE = 50

_HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:150.0) Gecko/20100101 Firefox/150.0",
}


def fetch_page(page: int) -> dict:
    url = f"{_FEED_BASE}?sid={_SID}&pagination={_PAGE_SIZE}&page={page}"
    response = requests.get(url, headers=_HEADERS, timeout=30)
    response.raise_for_status()
    return response.json()


def parse_items(data: dict) -> list[dict]:
    games = []
    for item in data.get("items", []):
        game_id = str(item["id"])
        category = item.get("category")
        games.append({
            "object_id": f"gp_{game_id}",
            "provider_game_id": game_id,
            "slug": item["namespace"],
            "title": item["title"],
            "description": item.get("description") or None,
            "instruction": None,
            "thumbnail_url": item["banner_image"],
            "game_url": item["url"],
            "categories": [category] if category else [],
            "first_active_date": item.get("date_published"),
            "provider": "GamePix",
            "company": None,
            "tags": [],
            "languages": [],
            "gender": [],
            "age_group": [],
        })
    return games


def has_next_page(data: dict) -> bool:
    return bool(data.get("next_url"))
