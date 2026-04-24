# scraper_service/db.py
from supabase import create_client, Client
from config import SUPABASE_URL, SUPABASE_KEY

_client: Client = create_client(SUPABASE_URL, SUPABASE_KEY)


def load_existing_object_ids() -> set[str]:
    """Load all object_ids (pending + done) for skip-checking."""
    result = _client.table("games").select("object_id").execute()
    return {row["object_id"] for row in result.data}


def load_pending_games() -> list[dict]:
    """Load all pending records to retry on startup."""
    result = (
        _client.table("games")
        .select("object_id,slug")
        .eq("status", "pending")
        .execute()
    )
    return result.data


def insert_pending(game: dict) -> None:
    """Insert a new game record with status=pending."""
    _client.table("games").insert(game).execute()


def update_done(object_id: str, detail: dict) -> None:
    """Update a game record with detail data and set status=done."""
    _client.table("games").update({**detail, "status": "done"}).eq(
        "object_id", object_id
    ).execute()


def load_games_needing_thumbnails() -> list[dict]:
    """Return done games whose thumbnail_url is still a remote URL."""
    result = (
        _client.table("games")
        .select("object_id,thumbnail_url")
        .eq("status", "done")
        .like("thumbnail_url", "http%")
        .execute()
    )
    return result.data


def update_thumbnail_url(object_id: str, local_url: str) -> None:
    """Update thumbnail_url to the locally-served path."""
    _client.table("games").update({"thumbnail_url": local_url}).eq(
        "object_id", object_id
    ).execute()
