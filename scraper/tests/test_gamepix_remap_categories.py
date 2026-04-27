import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import json
import pytest
from unittest.mock import patch, MagicMock
import gamepix_remap_categories as remap


# ── _remap_category_batch ─────────────────────────────────────────────────────

def test_remap_category_batch_raises_without_api_key():
    with patch.object(remap, "OPENAI_API_KEY", ""):
        with pytest.raises(ValueError, match="OPENAI_API_KEY"):
            remap._remap_category_batch([{"object_id": "gp_1", "title": "X", "description": "", "categories": ["action"]}])


def test_remap_category_batch_parses_valid_response():
    fake_response = {
        "choices": [{
            "message": {
                "content": json.dumps({
                    "mappings": [
                        {"object_id": "gp_1", "category": "Shooter"},
                        {"object_id": "gp_2", "category": "Puzzle"},
                    ]
                })
            }
        }]
    }
    mock_resp = MagicMock()
    mock_resp.raise_for_status = MagicMock()
    mock_resp.json.return_value = fake_response

    games = [
        {"object_id": "gp_1", "title": "Gun Game", "description": "Shoot things", "categories": ["action"]},
        {"object_id": "gp_2", "title": "Brain Game", "description": "Solve puzzles", "categories": ["puzzle"]},
    ]
    with patch.object(remap, "OPENAI_API_KEY", "sk-test"), \
         patch("gamepix_remap_categories.requests.post", return_value=mock_resp):
        result = remap._remap_category_batch(games)

    assert result == {"gp_1": "Shooter", "gp_2": "Puzzle"}


def test_remap_category_batch_ignores_non_canonical_response():
    fake_response = {
        "choices": [{
            "message": {
                "content": json.dumps({
                    "mappings": [
                        {"object_id": "gp_1", "category": "Action"},  # not in canonical list
                    ]
                })
            }
        }]
    }
    mock_resp = MagicMock()
    mock_resp.raise_for_status = MagicMock()
    mock_resp.json.return_value = fake_response

    games = [{"object_id": "gp_1", "title": "X", "description": "", "categories": ["action"]}]
    with patch.object(remap, "OPENAI_API_KEY", "sk-test"), \
         patch("gamepix_remap_categories.requests.post", return_value=mock_resp):
        result = remap._remap_category_batch(games)

    assert result == {}


# ── load_gamepix_games ────────────────────────────────────────────────────────

def test_load_gamepix_games_filters_only_unknown():
    all_games = [
        {"object_id": "gp_1", "title": "A", "description": "", "categories": ["action"]},
        {"object_id": "gp_2", "title": "B", "description": "", "categories": ["Shooter"]},  # already canonical
        {"object_id": "gp_3", "title": "C", "description": "", "categories": []},
    ]
    mock_result = MagicMock()
    mock_result.data = all_games

    mock_query = MagicMock()
    mock_query.eq.return_value = mock_query
    mock_query.range.return_value = mock_query
    mock_query.execute.return_value = mock_result

    mock_table = MagicMock()
    mock_table.select.return_value = mock_query

    with patch.object(remap._client, "table", return_value=mock_table):
        result = remap.load_gamepix_games(only_unknown=True, limit=None)

    # gp_2 has canonical "Shooter" — excluded
    assert [r["object_id"] for r in result] == ["gp_1", "gp_3"]


def test_load_gamepix_games_respects_limit():
    all_games = [
        {"object_id": f"gp_{i}", "title": f"Game {i}", "description": "", "categories": ["action"]}
        for i in range(20)
    ]
    mock_result = MagicMock()
    mock_result.data = all_games

    mock_query = MagicMock()
    mock_query.eq.return_value = mock_query
    mock_query.range.return_value = mock_query
    mock_query.execute.return_value = mock_result

    mock_table = MagicMock()
    mock_table.select.return_value = mock_query

    with patch.object(remap._client, "table", return_value=mock_table):
        result = remap.load_gamepix_games(only_unknown=False, limit=5)

    assert len(result) == 5


# ── _update_category ──────────────────────────────────────────────────────────

def test_update_category_calls_supabase():
    mock_update = MagicMock()
    mock_eq = MagicMock()
    mock_eq.execute = MagicMock()
    mock_update.eq.return_value = mock_eq

    mock_table = MagicMock()
    mock_table.update.return_value = mock_update

    with patch.object(remap._client, "table", return_value=mock_table):
        remap._update_category("gp_1", "Shooter")

    mock_table.update.assert_called_once_with({"categories": ["Shooter"]})
    mock_update.eq.assert_called_once_with("object_id", "gp_1")
    mock_eq.execute.assert_called_once()
