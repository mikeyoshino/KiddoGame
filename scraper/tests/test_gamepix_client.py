import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from gamepix_client import parse_items, has_next_page

_SAMPLE_FEED = {
    "next_url": "https://feeds.gamepix.com/v2/json?sid=22322&pagination=50&page=2",
    "items": [
        {
            "id": 12345,
            "title": "Angry Birds",
            "namespace": "angry-birds",
            "description": "A classic slingshot game.",
            "category": "Action",
            "banner_image": "https://cdn.gamepix.com/angry-birds.jpg",
            "image": "https://cdn.gamepix.com/angry-birds-icon.jpg",
            "url": "https://gamepix.com/play/angry-birds/",
            "date_published": "2020-03-01T00:00:00Z",
            "date_modified": "2024-01-01T00:00:00Z",
            "quality_score": 0.9,
            "orientation": "landscape",
            "width": 800,
            "height": 600,
        },
        {
            "id": 99999,
            "title": "No Category Game",
            "namespace": "no-cat",
            "description": None,
            "category": None,
            "banner_image": "https://cdn.gamepix.com/no-cat.jpg",
            "image": "https://cdn.gamepix.com/no-cat-icon.jpg",
            "url": "https://gamepix.com/play/no-cat/",
            "date_published": "2021-06-15T00:00:00Z",
            "date_modified": None,
            "quality_score": 0.5,
            "orientation": "portrait",
            "width": 400,
            "height": 700,
        },
    ],
}


def test_parse_items_maps_fields_correctly():
    games = parse_items(_SAMPLE_FEED)
    assert len(games) == 2

    g = games[0]
    assert g["object_id"] == "gp_12345"
    assert g["provider_game_id"] == "12345"
    assert g["slug"] == "angry-birds"
    assert g["title"] == "Angry Birds"
    assert g["description"] == "A classic slingshot game."
    assert g["instruction"] is None
    assert g["thumbnail_url"] == "https://cdn.gamepix.com/angry-birds.jpg"
    assert g["game_url"] == "https://gamepix.com/play/angry-birds/"
    assert g["categories"] == ["Action"]
    assert g["first_active_date"] == "2020-03-01T00:00:00Z"
    assert g["provider"] == "GamePix"
    assert g["company"] is None
    assert g["tags"] == []
    assert g["languages"] == []
    assert g["gender"] == []
    assert g["age_group"] == []


def test_parse_items_handles_null_category():
    games = parse_items(_SAMPLE_FEED)
    g = games[1]
    assert g["categories"] == []
    assert g["description"] is None


def test_parse_items_returns_empty_for_empty_feed():
    games = parse_items({"items": []})
    assert games == []


def test_has_next_page_returns_true_when_next_url_present():
    assert has_next_page(_SAMPLE_FEED) is True


def test_has_next_page_returns_false_when_absent():
    assert has_next_page({"items": []}) is False
    assert has_next_page({"next_url": None, "items": []}) is False
    assert has_next_page({"next_url": "", "items": []}) is False
