# tests/test_detail_fetcher.py
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import json
from detail_fetcher import parse_next_data

def _make_html(game_props: dict) -> str:
    next_data = {
        "props": {
            "pageProps": {
                "game": game_props
            }
        }
    }
    json_str = json.dumps(next_data)
    return f'<html><head><script id="__NEXT_DATA__" type="application/json">{json_str}</script></head><body></body></html>'


def test_parse_next_data_returns_all_fields():
    html = _make_html({
        "description": "A great game",
        "instruction": "Click to play",
        "tags": ["clicker", "idle"],
        "categories": ["Casual"],
        "languages": ["English", "French"],
        "gender": ["Male", "Female"],
        "ageGroup": ["Kids", "Teens"],
    })
    result = parse_next_data(html)
    assert result is not None
    assert result["description"] == "A great game"
    assert result["instruction"] == "Click to play"
    assert result["tags"] == ["clicker", "idle"]
    assert result["categories"] == ["Casual"]
    assert result["languages"] == ["English", "French"]
    assert result["gender"] == ["Male", "Female"]
    assert result["age_group"] == ["Kids", "Teens"]


def test_parse_next_data_handles_missing_fields():
    html = _make_html({"description": "Only desc"})
    result = parse_next_data(html)
    assert result is not None
    assert result["description"] == "Only desc"
    assert result["instruction"] is None
    assert result["tags"] == []
    assert result["age_group"] == []


def test_parse_next_data_returns_none_when_no_script():
    result = parse_next_data("<html><body>No data here</body></html>")
    assert result is None


def test_parse_next_data_returns_none_on_bad_json():
    html = '<script id="__NEXT_DATA__" type="application/json">{bad json}</script>'
    result = parse_next_data(html)
    assert result is None


def test_parse_next_data_returns_none_when_game_key_missing():
    next_data = json.dumps({"props": {"pageProps": {}}})
    html = f'<script id="__NEXT_DATA__" type="application/json">{next_data}</script>'
    result = parse_next_data(html)
    assert result is None
