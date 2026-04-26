import os
from pathlib import Path
from dotenv import load_dotenv

load_dotenv()

# Required for main.py scraping flow
WEBAPP_URL: str = os.environ["WEBAPP_URL"]
CONCURRENCY: int = int(os.getenv("CONCURRENCY", "5"))
PAGE_DELAY: float = float(os.getenv("PAGE_DELAY", "1.0"))

# Used only by standalone utility scripts (db.py, translate.py, thumbnail_downloader.py)
SUPABASE_URL: str = os.getenv("SUPABASE_URL", "")
SUPABASE_KEY: str = os.getenv("SUPABASE_KEY", "")
OPENAI_API_KEY: str = os.getenv("OPENAI_API_KEY", "")
_DEFAULT_THUMB_DIR = Path(__file__).parent.parent / "webapp" / "wwwroot" / "images" / "games"
THUMBNAIL_OUTPUT_DIR: Path = Path(os.getenv("THUMBNAIL_OUTPUT_DIR", str(_DEFAULT_THUMB_DIR)))
THUMBNAIL_URL_PREFIX: str = os.getenv("THUMBNAIL_URL_PREFIX", "/images/games")
THUMBNAIL_CONCURRENCY: int = int(os.getenv("THUMBNAIL_CONCURRENCY", "2"))
THUMBNAIL_DELAY: float = float(os.getenv("THUMBNAIL_DELAY", "0.5"))
