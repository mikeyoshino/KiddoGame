import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import json
import pytest
from pathlib import Path
from unittest.mock import patch, MagicMock
import gamepix_main


# ── Progress helpers ──────────────────────────────────────────────────────────

def test_load_progress_returns_zero_when_no_file(tmp_path):
    with patch.object(gamepix_main, "PROGRESS_FILE", tmp_path / "progress.json"):
        assert gamepix_main._load_progress() == 0


def test_save_and_load_progress(tmp_path):
    pf = tmp_path / "progress.json"
    with patch.object(gamepix_main, "PROGRESS_FILE", pf):
        gamepix_main._save_progress(7)
        assert gamepix_main._load_progress() == 7


def test_delete_progress_removes_file(tmp_path):
    pf = tmp_path / "progress.json"
    pf.write_text('{"last_completed_page": 3}')
    with patch.object(gamepix_main, "PROGRESS_FILE", pf):
        gamepix_main._delete_progress()
        assert not pf.exists()


def test_delete_progress_is_safe_when_no_file(tmp_path):
    with patch.object(gamepix_main, "PROGRESS_FILE", tmp_path / "missing.json"):
        gamepix_main._delete_progress()  # should not raise


# ── _translate_and_categorize_batch ──────────────────────────────────────────

def test_translate_and_categorize_batch_raises_without_api_key():
    with patch.object(gamepix_main, "OPENAI_API_KEY", ""):
        with pytest.raises(ValueError, match="OPENAI_API_KEY"):
            gamepix_main._translate_and_categorize_batch([
                {"object_id": "gp_1", "title": "X", "description": "Y", "categories": ["action"]}
            ])


def test_translate_and_categorize_batch_returns_description_th_and_category():
    fake_response = {
        "choices": [{
            "message": {
                "content": json.dumps({
                    "translations": [
                        {"object_id": "gp_1", "description_th": "เกมสนุก", "category": "Shooter"}
                    ]
                })
            }
        }]
    }
    mock_resp = MagicMock()
    mock_resp.raise_for_status = MagicMock()
    mock_resp.json.return_value = fake_response

    with patch.object(gamepix_main, "OPENAI_API_KEY", "sk-test"), \
         patch("gamepix_main.requests.post", return_value=mock_resp):
        result = gamepix_main._translate_and_categorize_batch([
            {"object_id": "gp_1", "title": "Gun Game", "description": "Shoot things", "categories": ["action"]}
        ])

    assert result == {"gp_1": {"description_th": "เกมสนุก", "category": "Shooter"}}


def test_translate_and_categorize_batch_handles_missing_entry():
    fake_response = {
        "choices": [{"message": {"content": json.dumps({"translations": []})}}]
    }
    mock_resp = MagicMock()
    mock_resp.raise_for_status = MagicMock()
    mock_resp.json.return_value = fake_response

    with patch.object(gamepix_main, "OPENAI_API_KEY", "sk-test"), \
         patch("gamepix_main.requests.post", return_value=mock_resp):
        result = gamepix_main._translate_and_categorize_batch([
            {"object_id": "gp_1", "title": "X", "description": "Y", "categories": ["action"]}
        ])

    assert result == {}
