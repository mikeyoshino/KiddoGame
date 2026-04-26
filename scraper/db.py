# scraper_service/db.py
from supabase import create_client, Client
from config import SUPABASE_URL, SUPABASE_KEY

_client: Client = create_client(SUPABASE_URL, SUPABASE_KEY)
_PAGE = 1000


def _all(query) -> list[dict]:
    """Fetch all rows from a select query, paginating past Supabase's 1000-row limit."""
    rows: list[dict] = []
    offset = 0
    while True:
        result = query.range(offset, offset + _PAGE - 1).execute()
        rows.extend(result.data)
        if len(result.data) < _PAGE:
            break
        offset += _PAGE
    return rows


def load_existing_object_ids() -> set[str]:
    """Load all object_ids (pending + done) for skip-checking."""
    rows = _all(_client.table("games").select("object_id"))
    return {row["object_id"] for row in rows}


def load_pending_games() -> list[dict]:
    """Load all pending records to retry on startup."""
    return _all(
        _client.table("games")
        .select("object_id,slug,thumbnail_url")
        .eq("status", "pending")
    )


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
    return _all(
        _client.table("games")
        .select("object_id,thumbnail_url")
        .eq("status", "done")
        .like("thumbnail_url", "http%")
    )


def update_thumbnail_url(object_id: str, local_url: str) -> None:
    """Update thumbnail_url to the locally-served path."""
    _client.table("games").update({"thumbnail_url": local_url}).eq(
        "object_id", object_id
    ).execute()


def set_status_pending(object_id: str) -> None:
    """Mark a record as pending so the thumbnail download is retried next run."""
    _client.table("games").update({"status": "pending"}).eq(
        "object_id", object_id
    ).execute()


def load_games_with_local_thumbnails() -> list[dict]:
    """Return done games whose thumbnail_url is a local path."""
    return _all(
        _client.table("games")
        .select("object_id,thumbnail_url")
        .eq("status", "done")
        .like("thumbnail_url", "/images/%")
    )


def reset_thumbnail_pending(object_id: str, remote_url: str) -> None:
    """Reset thumbnail_url to its remote URL and mark pending for re-download."""
    _client.table("games").update(
        {"status": "pending", "thumbnail_url": remote_url}
    ).eq("object_id", object_id).execute()


def mark_null_thumbnails_pending() -> None:
    """Reset done records with no thumbnail_url back to pending for retry."""
    (
        _client.table("games")
        .update({"status": "pending"})
        .eq("status", "done")
        .is_("thumbnail_url", "null")
        .execute()
    )
