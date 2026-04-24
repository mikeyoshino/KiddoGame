# tests/test_gd_client.py
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from gd_client import parse_hits, get_total_pages

SAMPLE_RESPONSE = {
    "data": {
        "gamesSearched": {
            "hitsPerPage": 30,
            "nbHits": 21344,
            "nbPages": 712,
            "page": 1,
            "hits": [
                {
                    "objectID": "de35402342e2480f824b75e44f7ac5ba",
                    "title": "Hard Puzzle",
                    "company": "Playgama",
                    "visible": True,
                    "slugs": [{"name": "hard-puzzle", "__typename": "SlugType"}],
                    "assets": [
                        {"name": "de35402342e2480f824b75e44f7ac5ba-512x384.jpg", "__typename": "AssetType"}
                    ],
                    "__typename": "SearchHit"
                },
                {
                    "objectID": "invisible001",
                    "title": "Hidden Game",
                    "company": "Nobody",
                    "visible": False,
                    "slugs": [{"name": "hidden-game", "__typename": "SlugType"}],
                    "assets": [],
                    "__typename": "SearchHit"
                },
                {
                    "objectID": "noslug001",
                    "title": "No Slug Game",
                    "company": "Nobody",
                    "visible": True,
                    "slugs": [],
                    "assets": [],
                    "__typename": "SearchHit"
                }
            ]
        }
    }
}


def test_parse_hits_returns_visible_games_with_slugs():
    result = parse_hits(SAMPLE_RESPONSE)
    assert len(result) == 1
    assert result[0]["object_id"] == "de35402342e2480f824b75e44f7ac5ba"


def test_parse_hits_fields():
    result = parse_hits(SAMPLE_RESPONSE)
    game = result[0]
    assert game["slug"] == "hard-puzzle"
    assert game["title"] == "Hard Puzzle"
    assert game["company"] == "Playgama"
    assert game["thumbnail_url"] == "https://img.gamedistribution.com/de35402342e2480f824b75e44f7ac5ba-512x384.jpg"
    assert game["status"] == "pending"


def test_parse_hits_skips_invisible():
    result = parse_hits(SAMPLE_RESPONSE)
    object_ids = [g["object_id"] for g in result]
    assert "invisible001" not in object_ids


def test_parse_hits_skips_no_slug():
    result = parse_hits(SAMPLE_RESPONSE)
    object_ids = [g["object_id"] for g in result]
    assert "noslug001" not in object_ids


def test_get_total_pages():
    assert get_total_pages(SAMPLE_RESPONSE) == 712
