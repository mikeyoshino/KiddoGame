# scraper_service/gd_client.py
import requests

GQL_URL = "https://gd-website-api.gamedistribution.com/graphql"

_HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:150.0) Gecko/20100101 Firefox/150.0",
    "Accept": "*/*",
    "content-type": "application/json",
    "apollographql-client-name": "GDWebSite",
    "apollographql-client-version": "1.0",
    "authorization": "",
    "Origin": "https://gamedistribution.com",
    "Referer": "https://gamedistribution.com/",
}

_QUERY = (
    "fragment CoreGame on SearchHit {\n"
    "  objectID\n  title\n  company\n  visible\n"
    "  slugs { name __typename }\n"
    "  assets { name __typename }\n"
    "  firstActiveDate\n"
    "  __typename\n}\n\n"
    "query GetGamesSearched($id: String! = \"\", $perPage: Int! = 0, $page: Int! = 0, "
    "$search: String! = \"\", $UIfilter: UIFilterInput! = {}, $filters: GameSearchFiltersFlat! = {}) {\n"
    "  gamesSearched(input: {collectionObjectId: $id, hitsPerPage: $perPage, page: $page, "
    "search: $search, UIfilter: $UIfilter, filters: $filters}) {\n"
    "    hitsPerPage nbHits nbPages page\n"
    "    hits { ...CoreGame __typename }\n"
    "    __typename\n  }\n}"
)


def fetch_page(page: int, per_page: int = 30, filters: dict | None = None) -> dict:
    payload = {
        "operationName": "GetGamesSearched",
        "variables": {
            "id": "", "perPage": per_page, "page": page,
            "search": "", "UIfilter": {}, "filters": filters or {}
        },
        "query": _QUERY,
    }
    response = requests.post(GQL_URL, json=payload, headers=_HEADERS, timeout=30)
    response.raise_for_status()
    return response.json()


def _pick_thumbnail(assets: list[dict], object_id: str) -> str:
    names = [a["name"] for a in assets]
    for size in ("512x384", "512x512", "200x120"):
        for name in names:
            if size in name:
                return f"https://img.gamedistribution.com/{name}"
    return f"https://img.gamedistribution.com/{object_id}-512x384.jpg"


def parse_hits(data: dict) -> list[dict]:
    try:
        hits = data["data"]["gamesSearched"]["hits"]
    except (KeyError, TypeError):
        import json
        preview = json.dumps(data)[:500]
        raise ValueError(f"Unexpected API response shape: {preview}")
    games = []
    for hit in hits:
        if not hit.get("visible"):
            continue
        slugs = hit.get("slugs", [])
        if not slugs:
            continue
        slug = slugs[0]["name"]
        object_id = hit["objectID"]
        games.append({
            "object_id": object_id,
            "slug": slug,
            "title": hit["title"],
            "company": hit.get("company"),
            "thumbnail_url": _pick_thumbnail(hit.get("assets", []), object_id),
            "first_active_date": hit.get("firstActiveDate"),
            "status": "pending",
        })
    return games


def get_total_pages(data: dict) -> int:
    return data["data"]["gamesSearched"]["nbPages"]
