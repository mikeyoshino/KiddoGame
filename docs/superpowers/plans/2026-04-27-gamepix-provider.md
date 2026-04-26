# GamePix Provider Integration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add GamePix as a second game provider using the same `games` table, with a new Python scraper that paginates all pages, deduplicates by title cross-provider, translates descriptions inline via OpenAI, and posts to the existing webapp ingest API.

**Architecture:** DB migration adds `provider`, `provider_game_id`, `game_url` columns. The C# webapp is minimally extended — `IngestGame` accepts pre-translated fields and skips OpenAI for GamePix games. A new `check-title-duplicates` endpoint enables cross-provider title dedup. The Python scraper (`gamepix_main.py`) handles pagination, dedup, inline translation, and progress checkpointing.

**Tech Stack:** Python 3.12, requests, aiohttp, ASP.NET Core 8, Supabase PostgREST, OpenAI gpt-4o-mini, pytest, xUnit

---

## File Map

**New files:**
- `scraper/sql/003_add_provider_fields.sql` — DB migration
- `scraper/gamepix_client.py` — feed fetcher + parser
- `scraper/gamepix_main.py` — orchestrator (pagination, dedup, translate, post)
- `scraper/tests/test_gamepix_client.py` — tests for feed parsing
- `scraper/tests/test_gamepix_main.py` — tests for translate helper + page processor

**Modified files:**
- `webapp/Models/IngestGame.cs` — add Provider, ProviderGameId, GameUrl, DescriptionTh, InstructionTh, TranslationStatus optional params
- `webapp/Models/Game.cs` — add Provider, ProviderGameId, StoredGameUrl; update GameUrl computed property
- `webapp/Services/IngestService.cs` — update TranslateBatchAsync (skip GamePix), UpsertGamesAsync (store new fields + use pre-translations), add CheckTitleDuplicatesAsync
- `webapp/Program.cs` — add `/api/ingest/check-title-duplicates` endpoint
- `scraper/webapp_client.py` — add check_title_duplicates function
- `scraper/tests/test_webapp_client.py` — add test for check_title_duplicates
- `webapp.Tests/IngestServiceTests.cs` — add tests for new IngestService behaviour

---

## Task 1: DB Migration

**Files:**
- Create: `scraper/sql/003_add_provider_fields.sql`

- [ ] **Step 1: Write the migration SQL**

```sql
-- scraper/sql/003_add_provider_fields.sql
ALTER TABLE games
  ADD COLUMN IF NOT EXISTS provider          TEXT NOT NULL DEFAULT 'GameDistribute',
  ADD COLUMN IF NOT EXISTS provider_game_id  TEXT,
  ADD COLUMN IF NOT EXISTS game_url          TEXT;

-- Backfill existing GD records
UPDATE games SET provider = 'GameDistribute' WHERE provider IS NULL OR provider = '';

CREATE INDEX IF NOT EXISTS idx_games_provider ON games (provider);
```

- [ ] **Step 2: Run the migration**

Open the Supabase dashboard → SQL Editor, paste the file contents, and run it.

Verify with:
```sql
SELECT provider, COUNT(*) FROM games GROUP BY provider;
-- Expected: one row: GameDistribute | <N>
SELECT column_name FROM information_schema.columns
WHERE table_name = 'games' AND column_name IN ('provider','provider_game_id','game_url');
-- Expected: 3 rows
```

- [ ] **Step 3: Commit**

```bash
git add scraper/sql/003_add_provider_fields.sql
git commit -m "feat: add provider, provider_game_id, game_url columns to games table"
```

---

## Task 2: Extend C# Models

**Files:**
- Modify: `webapp/Models/IngestGame.cs`
- Modify: `webapp/Models/Game.cs`

- [ ] **Step 1: Update IngestGame record**

Replace the entire content of `webapp/Models/IngestGame.cs`:

```csharp
namespace Kiddo.Web.Models;

public record IngestGame(
    string ObjectId,
    string Slug,
    string Title,
    string? Company,
    string ThumbnailUrl,
    string? Description,
    string? Instruction,
    string[] Categories,
    string[] Tags,
    string[] Languages,
    string[] Gender,
    string[] AgeGroup,
    string? FirstActiveDate = null,
    string Provider = "GameDistribute",
    string? ProviderGameId = null,
    string? GameUrl = null,
    string? DescriptionTh = null,
    string? InstructionTh = null,
    string? TranslationStatus = null
);

public record IngestBatchRequest(IngestGame[] Games);
public record IngestResult(string ObjectId, bool Ok, string? Error = null);
public record IngestBatchResponse(IngestResult[] Results);
```

- [ ] **Step 2: Update Game model**

Replace the entire content of `webapp/Models/Game.cs`:

```csharp
namespace Kiddo.Web.Models;

public class Game
{
    public string Id { get; set; } = "";
    public string ObjectId { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Company { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Description { get; set; }
    public string? Instruction { get; set; }
    public string? DescriptionTh { get; set; }
    public string? InstructionTh { get; set; }
    public string[] Categories { get; set; } = [];
    public string[] Tags { get; set; } = [];
    public string[] Languages { get; set; } = [];
    public string[] Gender { get; set; } = [];
    public string[] AgeGroup { get; set; } = [];
    public string Status { get; set; } = "";
    public int ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Provider { get; set; } = "GameDistribute";
    public string? ProviderGameId { get; set; }
    public string? StoredGameUrl { get; set; }

    public string GameUrl => StoredGameUrl
        ?? $"https://html5.gamedistribution.com/{ObjectId}/?gd_sdk_referrer_url=https://kiddogame.net/games/{Slug}/";
}
```

- [ ] **Step 3: Verify existing tests still compile and pass**

Run: `dotnet test /Users/mikeyoshino/gitRepos/KiddoGame/webapp.Tests`

Expected: all tests pass (new fields have defaults; existing callers use positional args up to `FirstActiveDate`).

- [ ] **Step 4: Commit**

```bash
git add webapp/Models/IngestGame.cs webapp/Models/Game.cs
git commit -m "feat: extend IngestGame and Game models with provider fields"
```

---

## Task 3: Update IngestService — TranslateBatchAsync

Skip OpenAI for GamePix games (or any game where `TranslationStatus` is already set).

**Files:**
- Modify: `webapp.Tests/IngestServiceTests.cs` (add test)
- Modify: `webapp/Services/IngestService.cs`

- [ ] **Step 1: Write the failing test**

Add this test to `IngestServiceTests` class in `webapp.Tests/IngestServiceTests.cs` (before the last closing `}`):

```csharp
[Fact]
public async Task TranslateBatchAsync_SkipsGamePixPreTranslatedGames()
{
    // AssertNotCalledHandler throws if HTTP is called — proves OpenAI is skipped
    var config = BuildConfig("/tmp", "/images", openAiKey: "sk-test");
    var factory = MakeFactory(new AssertNotCalledHandler());
    var svc = new IngestService(factory, config);

    var games = new[]
    {
        new IngestGame("gp_123", "cool", "Cool", null,
            "http://x.com/img.jpg", "Fun", null, [], [], [], [], [],
            Provider: "GamePix", TranslationStatus: "translated")
    };
    var result = await svc.TranslateBatchAsync(games);

    Assert.Empty(result);
}
```

Also add the `AssertNotCalledHandler` helper class near the bottom of `IngestServiceTests.cs` (before the final `}`):

```csharp
internal class AssertNotCalledHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage req, CancellationToken ct)
        => throw new InvalidOperationException("HTTP should not have been called");
}
```

- [ ] **Step 2: Run test to confirm it fails**

Run: `dotnet test /Users/mikeyoshino/gitRepos/KiddoGame/webapp.Tests --filter "TranslateBatchAsync_SkipsGamePixPreTranslatedGames"`

Expected: FAIL — `InvalidOperationException` (OpenAI is currently being called).

- [ ] **Step 3: Update TranslateBatchAsync to filter out pre-translated games**

In `webapp/Services/IngestService.cs`, find `TranslateBatchAsync` and replace its first two lines:

Old:
```csharp
    public async Task<Dictionary<string, (string? DescTh, string? InstrTh)>> TranslateBatchAsync(
        IngestGame[] games)
    {
        var openAiKey = config["Ingest:OpenAiApiKey"];
        if (string.IsNullOrEmpty(openAiKey)) return [];
```

New:
```csharp
    public async Task<Dictionary<string, (string? DescTh, string? InstrTh)>> TranslateBatchAsync(
        IngestGame[] games)
    {
        var openAiKey = config["Ingest:OpenAiApiKey"];
        // Only translate GD games that don't already have a translation
        games = games.Where(g => g.Provider == "GameDistribute" && g.TranslationStatus == null).ToArray();
        if (games.Length == 0 || string.IsNullOrEmpty(openAiKey)) return [];
```

- [ ] **Step 4: Run tests to confirm pass**

Run: `dotnet test /Users/mikeyoshino/gitRepos/KiddoGame/webapp.Tests`

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add webapp/Services/IngestService.cs webapp.Tests/IngestServiceTests.cs
git commit -m "feat: skip OpenAI translation for pre-translated GamePix games"
```

---

## Task 4: Update IngestService — UpsertGamesAsync

Store the three new columns and use pre-translated values for GamePix games.

**Files:**
- Modify: `webapp.Tests/IngestServiceTests.cs` (add test)
- Modify: `webapp/Services/IngestService.cs`

- [ ] **Step 1: Write the failing test**

Add to `IngestServiceTests` in `webapp.Tests/IngestServiceTests.cs`:

```csharp
[Fact]
public async Task UpsertGamesAsync_StoresProviderFieldsAndPreTranslation()
{
    var captured = new List<HttpRequestMessage>();
    var config = BuildConfig("/tmp", "/images",
        supabaseUrl: "https://fake.supabase.co", serviceKey: "svc");
    var factory = MakeFactory(new CapturingHandler("[]", captured));
    var svc = new IngestService(factory, config);

    var game = new IngestGame(
        "gp_123", "cool-game", "Cool Game", null,
        "http://img.gamepix.com/img.jpg", "Fun game", null,
        ["Action"], [], [], [], [],
        Provider: "GamePix",
        ProviderGameId: "123",
        GameUrl: "https://gamepix.com/play/cool-game",
        DescriptionTh: "เกมสนุก",
        InstructionTh: null,
        TranslationStatus: "translated"
    );

    await svc.UpsertGamesAsync(
        [game],
        new Dictionary<string, string?> { ["gp_123"] = "/images/games/gp_123.jpg" },
        []); // empty: webapp did NOT translate this

    var body = await captured[0].Content!.ReadAsStringAsync();
    Assert.Contains("\"provider\":\"GamePix\"", body);
    Assert.Contains("\"provider_game_id\":\"123\"", body);
    Assert.Contains("\"game_url\":\"https://gamepix.com/play/cool-game\"", body);
    Assert.Contains("\"description_th\":\"เกมสนุก\"", body);
    Assert.Contains("\"translation_status\":\"translated\"", body);
}
```

- [ ] **Step 2: Run test to confirm it fails**

Run: `dotnet test /Users/mikeyoshino/gitRepos/KiddoGame/webapp.Tests --filter "UpsertGamesAsync_StoresProviderFieldsAndPreTranslation"`

Expected: FAIL — body does not contain `provider`, `provider_game_id`, `game_url` fields.

- [ ] **Step 3: Update UpsertGamesAsync**

In `webapp/Services/IngestService.cs`, find `UpsertGamesAsync` and replace the `rows` LINQ projection. Replace from `var rows = games.Select(g =>` to the closing `}).ToArray();`:

Old:
```csharp
        var rows = games.Select(g =>
        {
            var thumbOk = thumbnails.TryGetValue(g.ObjectId, out var localUrl) && localUrl != null;
            var hasTrans = translations.TryGetValue(g.ObjectId, out var tr);
            return new
            {
                object_id = g.ObjectId,
                slug = g.Slug,
                title = g.Title,
                company = g.Company,
                thumbnail_url = thumbOk ? localUrl : g.ThumbnailUrl,
                description = g.Description,
                instruction = g.Instruction,
                description_th = hasTrans ? tr.DescTh : (string?)null,
                instruction_th = hasTrans ? tr.InstrTh : (string?)null,
                translation_status = hasTrans ? "translated" : (string?)null,
                categories = g.Categories,
                tags = g.Tags,
                languages = g.Languages,
                gender = g.Gender,
                age_group = g.AgeGroup,
                status = thumbOk ? "done" : "pending",
                created_at = g.FirstActiveDate,
            };
        }).ToArray();
```

New:
```csharp
        var rows = games.Select(g =>
        {
            var thumbOk = thumbnails.TryGetValue(g.ObjectId, out var localUrl) && localUrl != null;
            var preTranslated = g.TranslationStatus != null;
            var hasTrans = !preTranslated && translations.TryGetValue(g.ObjectId, out var tr);
            return new
            {
                object_id = g.ObjectId,
                slug = g.Slug,
                title = g.Title,
                company = g.Company,
                thumbnail_url = thumbOk ? localUrl : g.ThumbnailUrl,
                description = g.Description,
                instruction = g.Instruction,
                description_th = preTranslated ? g.DescriptionTh : (hasTrans ? tr.DescTh : null),
                instruction_th = preTranslated ? g.InstructionTh : (hasTrans ? tr.InstrTh : null),
                translation_status = preTranslated ? g.TranslationStatus : (hasTrans ? "translated" : (string?)null),
                categories = g.Categories,
                tags = g.Tags,
                languages = g.Languages,
                gender = g.Gender,
                age_group = g.AgeGroup,
                status = thumbOk ? "done" : "pending",
                created_at = g.FirstActiveDate,
                provider = g.Provider,
                provider_game_id = g.ProviderGameId,
                game_url = g.GameUrl,
            };
        }).ToArray();
```

- [ ] **Step 4: Run all tests**

Run: `dotnet test /Users/mikeyoshino/gitRepos/KiddoGame/webapp.Tests`

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add webapp/Services/IngestService.cs webapp.Tests/IngestServiceTests.cs
git commit -m "feat: store provider fields and pre-translations in UpsertGamesAsync"
```

---

## Task 5: Add CheckTitleDuplicatesAsync + Endpoint

**Files:**
- Modify: `webapp.Tests/IngestServiceTests.cs` (add tests)
- Modify: `webapp/Services/IngestService.cs` (add method)
- Modify: `webapp/Program.cs` (add endpoint)

- [ ] **Step 1: Write failing tests**

Add to `IngestServiceTests` in `webapp.Tests/IngestServiceTests.cs`:

```csharp
// ── CheckTitleDuplicatesAsync ─────────────────────────────────────────────

[Fact]
public async Task CheckTitleDuplicatesAsync_ReturnsExistingTitles()
{
    var supabaseJson = """[{"title":"Angry Birds"},{"title":"Fruit Ninja"}]""";
    var config = BuildConfig("/tmp", "/images",
        supabaseUrl: "https://fake.supabase.co", serviceKey: "svc");
    var factory = MakeFactory(new FakeHandler(supabaseJson));
    var svc = new IngestService(factory, config);

    var result = await svc.CheckTitleDuplicatesAsync(["Angry Birds", "New Game", "Fruit Ninja"]);

    Assert.Equal(2, result.Length);
    Assert.Contains("Angry Birds", result);
    Assert.Contains("Fruit Ninja", result);
}

[Fact]
public async Task CheckTitleDuplicatesAsync_EmptyInputReturnsEmpty()
{
    var config = BuildConfig("/tmp", "/images",
        supabaseUrl: "https://fake.supabase.co", serviceKey: "svc");
    var factory = MakeFactory(new AssertNotCalledHandler());
    var svc = new IngestService(factory, config);

    var result = await svc.CheckTitleDuplicatesAsync([]);

    Assert.Empty(result);
}
```

- [ ] **Step 2: Run tests to confirm they fail**

Run: `dotnet test /Users/mikeyoshino/gitRepos/KiddoGame/webapp.Tests --filter "CheckTitleDuplicatesAsync"`

Expected: FAIL — method does not exist.

- [ ] **Step 3: Add CheckTitleDuplicatesAsync to IngestService**

Add this method to `IngestService` in `webapp/Services/IngestService.cs` (before the closing `}`):

```csharp
    public async Task<string[]> CheckTitleDuplicatesAsync(string[] titles)
    {
        if (titles.Length == 0) return [];

        var supabaseUrl = config["Supabase:Url"]!;
        var serviceKey = config["Supabase:ServiceKey"]!;
        var quotedTitles = string.Join(",", titles.Select(t => $"\"{t.Replace("\"", "\\\"")}\""));

        var client = httpFactory.CreateClient("supabase-ingest");
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{supabaseUrl}/rest/v1/games?select=title&title=in.({quotedTitles})");
        request.Headers.Add("apikey", serviceKey);
        request.Headers.Add("Authorization", $"Bearer {serviceKey}");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        return doc.RootElement.EnumerateArray()
            .Select(r => r.GetProperty("title").GetString()!)
            .ToArray();
    }
```

- [ ] **Step 4: Add the endpoint to Program.cs**

In `webapp/Program.cs`, add this block after the existing `filter-new` endpoint (after its `.DisableAntiforgery();` line):

```csharp
app.MapPost("/api/ingest/check-title-duplicates", async (
    [Microsoft.AspNetCore.Mvc.FromBody] string[] titles, IngestService ingestSvc) =>
{
    var duplicates = await ingestSvc.CheckTitleDuplicatesAsync(titles);
    return Results.Ok(duplicates);
}).DisableAntiforgery();
```

- [ ] **Step 5: Run all tests**

Run: `dotnet test /Users/mikeyoshino/gitRepos/KiddoGame/webapp.Tests`

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add webapp/Services/IngestService.cs webapp/Program.cs webapp.Tests/IngestServiceTests.cs
git commit -m "feat: add CheckTitleDuplicatesAsync and /api/ingest/check-title-duplicates endpoint"
```

---

## Task 6: Add check_title_duplicates to webapp_client.py

**Files:**
- Modify: `scraper/webapp_client.py`
- Modify: `scraper/tests/test_webapp_client.py`

- [ ] **Step 1: Write the failing test**

Add to `scraper/tests/test_webapp_client.py`:

```python
@pytest.mark.asyncio
async def test_check_title_duplicates_returns_existing_titles():
    with aioresponses() as m:
        m.post("http://testapp/api/ingest/check-title-duplicates",
               payload=["Angry Birds", "Fruit Ninja"])
        with patch.object(webapp_client, "WEBAPP_URL", "http://testapp"):
            result = await webapp_client.check_title_duplicates(
                ["Angry Birds", "New Game", "Fruit Ninja"]
            )
    assert result == ["Angry Birds", "Fruit Ninja"]


@pytest.mark.asyncio
async def test_check_title_duplicates_returns_empty_for_empty_input():
    result = await webapp_client.check_title_duplicates([])
    assert result == []
```

- [ ] **Step 2: Run tests to confirm they fail**

Run: `cd /Users/mikeyoshino/gitRepos/KiddoGame/scraper && python -m pytest tests/test_webapp_client.py::test_check_title_duplicates_returns_existing_titles -v`

Expected: FAIL — `AttributeError: module 'webapp_client' has no attribute 'check_title_duplicates'`

- [ ] **Step 3: Add the function to webapp_client.py**

Add to `scraper/webapp_client.py` (after the existing `post_batch` function):

```python
_TITLE_TIMEOUT = aiohttp.ClientTimeout(total=30)


async def check_title_duplicates(titles: list[str]) -> list[str]:
    """Return titles from the given list that already exist in the DB."""
    if not titles:
        return []
    async with aiohttp.ClientSession() as session:
        async with session.post(
            f"{WEBAPP_URL}/api/ingest/check-title-duplicates",
            json=titles,
            timeout=_TITLE_TIMEOUT,
        ) as resp:
            resp.raise_for_status()
            return await resp.json()
```

- [ ] **Step 4: Run tests to confirm they pass**

Run: `cd /Users/mikeyoshino/gitRepos/KiddoGame/scraper && python -m pytest tests/test_webapp_client.py -v`

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add scraper/webapp_client.py scraper/tests/test_webapp_client.py
git commit -m "feat: add check_title_duplicates to webapp_client"
```

---

## Task 7: gamepix_client.py

**Files:**
- Create: `scraper/gamepix_client.py`
- Create: `scraper/tests/test_gamepix_client.py`

- [ ] **Step 1: Write failing tests**

Create `scraper/tests/test_gamepix_client.py`:

```python
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
```

- [ ] **Step 2: Run tests to confirm they fail**

Run: `cd /Users/mikeyoshino/gitRepos/KiddoGame/scraper && python -m pytest tests/test_gamepix_client.py -v`

Expected: FAIL — `ModuleNotFoundError: No module named 'gamepix_client'`

- [ ] **Step 3: Create gamepix_client.py**

Create `scraper/gamepix_client.py`:

```python
import requests

_FEED_BASE = "https://feeds.gamepix.com/v2/json"
_SID = "22322"
_PAGE_SIZE = 50

_HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:150.0) Gecko/20100101 Firefox/150.0",
}


def fetch_page(page: int) -> dict:
    url = f"{_FEED_BASE}?sid={_SID}&pagination={_PAGE_SIZE}&page={page}"
    response = requests.get(url, headers=_HEADERS, timeout=30)
    response.raise_for_status()
    return response.json()


def parse_items(data: dict) -> list[dict]:
    games = []
    for item in data.get("items", []):
        game_id = str(item["id"])
        category = item.get("category")
        games.append({
            "object_id": f"gp_{game_id}",
            "provider_game_id": game_id,
            "slug": item["namespace"],
            "title": item["title"],
            "description": item.get("description") or None,
            "instruction": None,
            "thumbnail_url": item["banner_image"],
            "game_url": item["url"],
            "categories": [category] if category else [],
            "first_active_date": item.get("date_published"),
            "provider": "GamePix",
            "company": None,
            "tags": [],
            "languages": [],
            "gender": [],
            "age_group": [],
        })
    return games


def has_next_page(data: dict) -> bool:
    return bool(data.get("next_url"))
```

- [ ] **Step 4: Run tests to confirm they pass**

Run: `cd /Users/mikeyoshino/gitRepos/KiddoGame/scraper && python -m pytest tests/test_gamepix_client.py -v`

Expected: all 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add scraper/gamepix_client.py scraper/tests/test_gamepix_client.py
git commit -m "feat: add gamepix_client with feed fetcher and parser"
```

---

## Task 8: gamepix_main.py

**Files:**
- Create: `scraper/gamepix_main.py`
- Create: `scraper/tests/test_gamepix_main.py`

- [ ] **Step 1: Write failing tests**

Create `scraper/tests/test_gamepix_main.py`:

```python
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


# ── Translation helper ────────────────────────────────────────────────────────

def test_translate_batch_raises_without_api_key():
    with patch.object(gamepix_main, "OPENAI_API_KEY", ""):
        with pytest.raises(ValueError, match="OPENAI_API_KEY"):
            gamepix_main._translate_batch([{"object_id": "gp_1", "title": "X", "description": "Y"}])


def test_translate_batch_parses_response():
    fake_response = {
        "choices": [{
            "message": {
                "content": json.dumps({
                    "translations": [
                        {"object_id": "gp_1", "description_th": "เกมสนุก"}
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
        result = gamepix_main._translate_batch([
            {"object_id": "gp_1", "title": "Cool", "description": "Fun game"}
        ])

    assert result == {"gp_1": "เกมสนุก"}


def test_translate_batch_returns_none_for_missing_translation():
    fake_response = {
        "choices": [{"message": {"content": json.dumps({"translations": []})}}]
    }
    mock_resp = MagicMock()
    mock_resp.raise_for_status = MagicMock()
    mock_resp.json.return_value = fake_response

    with patch.object(gamepix_main, "OPENAI_API_KEY", "sk-test"), \
         patch("gamepix_main.requests.post", return_value=mock_resp):
        result = gamepix_main._translate_batch([
            {"object_id": "gp_1", "title": "Cool", "description": "Fun"}
        ])

    assert result == {}
```

- [ ] **Step 2: Run tests to confirm they fail**

Run: `cd /Users/mikeyoshino/gitRepos/KiddoGame/scraper && python -m pytest tests/test_gamepix_main.py -v`

Expected: FAIL — `ModuleNotFoundError: No module named 'gamepix_main'`

- [ ] **Step 3: Create gamepix_main.py**

Create `scraper/gamepix_main.py`:

```python
import asyncio
import json
import sys
import textwrap
import time
from pathlib import Path

import requests

from config import OPENAI_API_KEY, PAGE_DELAY
from gamepix_client import fetch_page, parse_items, has_next_page
from webapp_client import filter_new, post_batch, check_title_duplicates

PROGRESS_FILE = Path(__file__).parent / "gamepix_progress.json"
_BATCH_SIZE = 10
_OPENAI_URL = "https://api.openai.com/v1/chat/completions"
_OPENAI_MODEL = "gpt-4o-mini"

_SYSTEM_PROMPT = textwrap.dedent("""
    You are a professional Thai translator for a kids' game website.
    Rules:
    1. Translate "description" from English into Thai.
    2. Game titles (proper nouns) MUST remain in English exactly as-is.
    3. Keep translations natural, friendly, suitable for children aged 5-12.
    4. Return ONLY a JSON object with key "translations" containing an array.
    5. Each item: {"object_id": "...", "description_th": "Thai text or null"}.
    6. Do NOT add explanation, markdown, or extra text.
""").strip()


def _load_progress() -> int:
    if PROGRESS_FILE.exists():
        data = json.loads(PROGRESS_FILE.read_text())
        return data.get("last_completed_page", 0)
    return 0


def _save_progress(page: int) -> None:
    PROGRESS_FILE.write_text(json.dumps({"last_completed_page": page}))


def _delete_progress() -> None:
    PROGRESS_FILE.unlink(missing_ok=True)


def _translate_batch(games: list[dict]) -> dict[str, str | None]:
    """Translate descriptions. Returns {object_id: description_th}. Raises on failure."""
    if not OPENAI_API_KEY:
        raise ValueError("OPENAI_API_KEY not set in .env")

    items = [
        {"object_id": g["object_id"], "title": g["title"], "description": g.get("description") or ""}
        for g in games
    ]
    user_msg = (
        "Return translations in a JSON object with key 'translations' containing the array:\n"
        + json.dumps(items, ensure_ascii=False)
    )
    payload = {
        "model": _OPENAI_MODEL,
        "messages": [
            {"role": "system", "content": _SYSTEM_PROMPT},
            {"role": "user", "content": user_msg},
        ],
        "response_format": {"type": "json_object"},
        "temperature": 0.2,
    }
    headers = {
        "Authorization": f"Bearer {OPENAI_API_KEY}",
        "Content-Type": "application/json",
    }

    resp = requests.post(_OPENAI_URL, json=payload, headers=headers, timeout=60)
    resp.raise_for_status()

    content = resp.json()["choices"][0]["message"]["content"]
    data = json.loads(content)
    return {item["object_id"]: item.get("description_th") for item in data.get("translations", [])}


async def _process_page(games: list[dict]) -> bool:
    """Filter, translate, and post one page of games. Returns False if script should stop."""
    object_ids = [g["object_id"] for g in games]
    new_ids = set(await filter_new(object_ids))
    new_games = [g for g in games if g["object_id"] in new_ids]

    if not new_games:
        print(f"  -> 0 new, {len(games)} already known")
        return True

    titles = [g["title"] for g in new_games]
    duplicate_titles = set(await check_title_duplicates(titles))
    unique_games = [g for g in new_games if g["title"] not in duplicate_titles]

    skipped_titles = len(new_games) - len(unique_games)
    if skipped_titles:
        print(f"  -> {skipped_titles} skipped (title exists in DB from another provider)")

    if not unique_games:
        return True

    # Translate all games for this page upfront; stop on failure
    translation_failed = False
    translation_map: dict[str, str | None] = {}

    for i in range(0, len(unique_games), _BATCH_SIZE):
        batch = unique_games[i: i + _BATCH_SIZE]
        try:
            result = _translate_batch(batch)
            translation_map.update(result)
        except Exception as e:
            print(f"  [ERROR] Translation failed: {e}")
            translation_failed = True
            break

    # Attach translations (null for un-translated games if failure occurred)
    for g in unique_games:
        g["description_th"] = translation_map.get(g["object_id"]) if not translation_failed else None
        g["instruction_th"] = None
        g["translation_status"] = "translated" if g["object_id"] in translation_map else None

    # Post all games (even those with null translation — webapp just stores them)
    for i in range(0, len(unique_games), _BATCH_SIZE):
        batch = unique_games[i: i + _BATCH_SIZE]
        results = await post_batch(batch)
        for r in results:
            status = "OK" if r["ok"] else f"FAIL: {r.get('error', 'unknown')}"
            print(f"  [{status}] {r['object_id']}")

    print(f"  -> {len(unique_games)} ingested ({len(translation_map)} translated)")
    return not translation_failed


async def main() -> None:
    start_page = _load_progress() + 1
    print(f"Starting GamePix scrape from page {start_page}...")

    page = start_page
    while True:
        print(f"Page {page}...")
        try:
            data = fetch_page(page)
        except Exception as e:
            print(f"  [ERROR] Failed to fetch page {page}: {e}")
            break

        games = parse_items(data)
        if not games:
            print("  -> empty page, done")
            break

        ok = await _process_page(games)
        _save_progress(page)

        if not ok:
            print(
                f"\nTranslation failed. Progress saved at page {page}.\n"
                "Re-run this script to resume from here.\n"
                "Or run 'python translate.py' to translate games with null translation_status."
            )
            sys.exit(1)

        if not has_next_page(data):
            print("No more pages.")
            break

        page += 1
        time.sleep(PAGE_DELAY)

    print("Scrape complete.")
    _delete_progress()


if __name__ == "__main__":
    asyncio.run(main())
```

- [ ] **Step 4: Run tests to confirm they pass**

Run: `cd /Users/mikeyoshino/gitRepos/KiddoGame/scraper && python -m pytest tests/test_gamepix_main.py -v`

Expected: all 7 tests pass.

- [ ] **Step 5: Run the full test suite**

Run: `cd /Users/mikeyoshino/gitRepos/KiddoGame/scraper && python -m pytest tests/ -v`

Expected: all scraper tests pass.

Run: `dotnet test /Users/mikeyoshino/gitRepos/KiddoGame/webapp.Tests`

Expected: all C# tests pass.

- [ ] **Step 6: Commit**

```bash
git add scraper/gamepix_main.py scraper/tests/test_gamepix_main.py
git commit -m "feat: add gamepix_main scraper with pagination, title dedup, inline translation, and progress checkpointing"
```

---

## Task 9: Deploy

- [ ] **Step 1: Rebuild and redeploy the webapp**

On the server:
```bash
git pull
docker compose build && docker compose up -d
```

- [ ] **Step 2: Verify the new endpoints work**

```bash
# Should return [] (no duplicates for a new title)
curl -s -X POST https://kiddogame.net/api/ingest/check-title-duplicates \
  -H "Content-Type: application/json" \
  -d '["Some Brand New Title"]'
# Expected: []
```

- [ ] **Step 3: Run GamePix scraper**

```bash
cd /path/to/scraper
python gamepix_main.py
```

Watch for `[OK]` lines. If it stops with a translation error, run `python translate.py` to catch up translations, then re-run `python gamepix_main.py` to continue.
