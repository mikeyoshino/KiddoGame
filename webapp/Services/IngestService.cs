using System.Text.Json;
using Kiddo.Web.Models;
using Microsoft.Extensions.Configuration;

namespace Kiddo.Web.Services;

public class IngestService(IHttpClientFactory httpFactory, IConfiguration config)
{
    private static readonly string[] _extensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    public async Task<string?> DownloadThumbnailAsync(string objectId, string remoteUrl)
    {
        var thumbnailDir = config["Ingest:ThumbnailDir"]!;
        var urlPrefix = config["Ingest:ThumbnailUrlPrefix"]!;

        var lastDot = remoteUrl.LastIndexOf('.');
        var baseUrl = lastDot >= 0 ? remoteUrl[..lastDot] : remoteUrl;
        var currentExt = lastDot >= 0 ? remoteUrl[lastDot..].ToLower() : "";

        var candidates = new List<string> { remoteUrl };
        foreach (var ext in _extensions)
            if (ext != currentExt) candidates.Add(baseUrl + ext);

        var client = httpFactory.CreateClient("thumbnail");
        foreach (var url in candidates)
        {
            try
            {
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) continue;

                var data = await response.Content.ReadAsByteArrayAsync();
                var ext = Path.GetExtension(new Uri(url).AbsolutePath);
                if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                var filename = $"{objectId}{ext}";

                Directory.CreateDirectory(thumbnailDir);
                await File.WriteAllBytesAsync(Path.Combine(thumbnailDir, filename), data);
                return $"{urlPrefix}/{filename}";
            }
            catch { continue; }
        }
        return null;
    }

    private const string _systemPrompt = """
        You are a professional Thai translator for a kids' game website.
        Rules you MUST follow:
        1. Translate "description" and "instruction" from English into Thai.
        2. Game titles (proper nouns) MUST remain in English exactly as-is.
        3. Keep translations natural, friendly, suitable for children aged 5-12.
        4. Return ONLY a valid JSON object with key 'translations' containing an array.
        5. Each item must have: "object_id" (copy unchanged), "description_th" (Thai or null), "instruction_th" (Thai or null).
        6. Do NOT include any explanation, markdown, or extra text.
        """;

    public async Task<Dictionary<string, (string? DescTh, string? InstrTh)>> TranslateBatchAsync(
        IngestGame[] games)
    {
        var openAiKey = config["Ingest:OpenAiApiKey"];
        if (string.IsNullOrEmpty(openAiKey)) return [];

        var items = games.Select(g => new
        {
            object_id = g.ObjectId,
            title = g.Title,
            description = g.Description ?? "",
            instruction = g.Instruction ?? "",
        });
        var userMsg = $"Return translations in a JSON object with key 'translations' containing the array:\n{JsonSerializer.Serialize(items)}";
        var payload = new
        {
            model = "gpt-4o-mini",
            messages = new[]
            {
                new { role = "system", content = _systemPrompt },
                new { role = "user", content = userMsg },
            },
            response_format = new { type = "json_object" },
            temperature = 0.2,
        };

        try
        {
            var client = httpFactory.CreateClient("openai");
            var response = await client.PostAsJsonAsync(
                "https://api.openai.com/v1/chat/completions", payload);
            if (!response.IsSuccessStatusCode) return [];
            return ParseTranslations(await response.Content.ReadAsStringAsync());
        }
        catch { return []; }
    }

    private static Dictionary<string, (string? DescTh, string? InstrTh)> ParseTranslations(
        string rawResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawResponse);
            var messageContent = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()!;

            using var inner = JsonDocument.Parse(messageContent);
            var result = new Dictionary<string, (string?, string?)>();
            foreach (var item in inner.RootElement.GetProperty("translations").EnumerateArray())
            {
                var id = item.GetProperty("object_id").GetString()!;
                var descTh = item.TryGetProperty("description_th", out var d)
                    && d.ValueKind != JsonValueKind.Null ? d.GetString() : null;
                var instrTh = item.TryGetProperty("instruction_th", out var i)
                    && i.ValueKind != JsonValueKind.Null ? i.GetString() : null;
                result[id] = (descTh, instrTh);
            }
            return result;
        }
        catch { return []; }
    }

    public async Task<string[]> FilterNewAsync(string[] ids)
    {
        if (ids.Length == 0) return [];

        var supabaseUrl = config["Supabase:Url"]!;
        var serviceKey = config["Supabase:ServiceKey"]!;
        var inList = string.Join(",", ids);

        var client = httpFactory.CreateClient("supabase-ingest");
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{supabaseUrl}/rest/v1/games?select=object_id,status&object_id=in.({inList})");
        request.Headers.Add("apikey", serviceKey);
        request.Headers.Add("Authorization", $"Bearer {serviceKey}");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        var doneIds = doc.RootElement.EnumerateArray()
            .Where(r => r.GetProperty("status").GetString() == "done")
            .Select(r => r.GetProperty("object_id").GetString()!)
            .ToHashSet();

        return ids.Where(id => !doneIds.Contains(id)).ToArray();
    }

    public async Task UpsertGamesAsync(
        IngestGame[] games,
        Dictionary<string, string?> thumbnails,
        Dictionary<string, (string? DescTh, string? InstrTh)> translations)
    {
        var supabaseUrl = config["Supabase:Url"]!;
        var serviceKey = config["Supabase:ServiceKey"]!;

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
            };
        }).ToArray();

        var client = httpFactory.CreateClient("supabase-ingest");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{supabaseUrl}/rest/v1/games")
        {
            Content = JsonContent.Create(rows),
        };
        request.Headers.Add("apikey", serviceKey);
        request.Headers.Add("Authorization", $"Bearer {serviceKey}");
        request.Headers.Add("Prefer", "resolution=merge-duplicates");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
