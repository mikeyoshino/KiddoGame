import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import pytest
from unittest.mock import patch
from aioresponses import aioresponses
import webapp_client


@pytest.mark.asyncio
async def test_filter_new_returns_subset_from_server():
    with aioresponses() as m:
        m.post("http://testapp/api/ingest/filter-new", payload=["id2", "id3"])
        with patch.object(webapp_client, "WEBAPP_URL", "http://testapp"):
            result = await webapp_client.filter_new(["id1", "id2", "id3"])
    assert result == ["id2", "id3"]


@pytest.mark.asyncio
async def test_filter_new_returns_empty_for_empty_input():
    result = await webapp_client.filter_new([])
    assert result == []


@pytest.mark.asyncio
async def test_post_batch_returns_results():
    with aioresponses() as m:
        m.post("http://testapp/api/ingest/batch", payload={
            "results": [{"object_id": "abc", "ok": True, "error": None}]
        })
        with patch.object(webapp_client, "WEBAPP_URL", "http://testapp"):
            result = await webapp_client.post_batch([{
                "object_id": "abc", "slug": "cool", "title": "Cool",
                "company": None, "thumbnail_url": "https://img.example.com/img.jpg",
                "description": None, "instruction": None,
                "categories": [], "tags": [], "languages": [], "gender": [], "age_group": []
            }])
    assert result == [{"object_id": "abc", "ok": True, "error": None}]


@pytest.mark.asyncio
async def test_post_batch_raises_on_server_error():
    with aioresponses() as m:
        m.post("http://testapp/api/ingest/batch", status=500)
        with patch.object(webapp_client, "WEBAPP_URL", "http://testapp"):
            with pytest.raises(Exception):
                await webapp_client.post_batch([{"object_id": "abc"}])
