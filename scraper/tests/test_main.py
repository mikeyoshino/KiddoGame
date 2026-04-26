import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import pytest
from unittest.mock import patch
import main


@pytest.mark.asyncio
async def test_send_batches_chunks_into_groups_of_10():
    calls = []

    async def fake_post_batch(games):
        calls.append(len(games))
        return [{"object_id": g["object_id"], "ok": True, "error": None} for g in games]

    with patch.object(main, "post_batch", side_effect=fake_post_batch):
        games = [{"object_id": str(i)} for i in range(25)]
        await main._send_batches(games)

    assert calls == [10, 10, 5]


@pytest.mark.asyncio
async def test_send_batches_empty_does_nothing():
    called = False

    async def fake_post_batch(games):
        nonlocal called
        called = True
        return []

    with patch.object(main, "post_batch", side_effect=fake_post_batch):
        await main._send_batches([])

    assert not called


@pytest.mark.asyncio
async def test_send_batches_logs_failures(capsys):
    async def fake_post_batch(games):
        return [{"object_id": g["object_id"], "ok": False, "error": "thumbnail: all extensions failed"} for g in games]

    with patch.object(main, "post_batch", side_effect=fake_post_batch):
        await main._send_batches([{"object_id": "abc"}])

    captured = capsys.readouterr()
    assert "FAIL" in captured.out
    assert "abc" in captured.out
