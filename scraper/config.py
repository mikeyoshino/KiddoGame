# scraper_service/config.py
import os
from pathlib import Path
from dotenv import load_dotenv

load_dotenv()

SUPABASE_URL: str = os.environ["SUPABASE_URL"]
SUPABASE_KEY: str = os.environ["SUPABASE_KEY"]
CONCURRENCY: int = int(os.getenv("CONCURRENCY", "5"))
PAGE_DELAY: float = float(os.getenv("PAGE_DELAY", "1.0"))

# Absolute path to the Blazor wwwroot/images/games folder.
# Override via THUMBNAIL_OUTPUT_DIR env var if needed.
_DEFAULT_THUMB_DIR = Path(__file__).parent.parent / "webapp" / "wwwroot" / "images" / "games"
THUMBNAIL_OUTPUT_DIR: Path = Path(os.getenv("THUMBNAIL_OUTPUT_DIR", str(_DEFAULT_THUMB_DIR)))
# Web-accessible URL prefix served by Blazor UseStaticFiles()
THUMBNAIL_URL_PREFIX: str = os.getenv("THUMBNAIL_URL_PREFIX", "/images/games")
