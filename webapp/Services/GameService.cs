using System.Text.Json;
using Kiddo.Web.Models;

namespace Kiddo.Web.Services;

public class GameService(HttpClient http)
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    internal static int ParseTotal(string? contentRange)
    {
        if (contentRange is null) return 0;
        var parts = contentRange.Split('/');
        return parts.Length == 2 && int.TryParse(parts[1], out var total) ? total : 0;
    }

    public async Task<(List<Game> Games, int Total)> GetGamesAsync(
        int page, int pageSize = 30, string? category = null, string? search = null)
    {
        var offset = (page - 1) * pageSize;
        var url = $"games?select=*&status=eq.done&order=created_at.desc&offset={offset}&limit={pageSize}";

        if (!string.IsNullOrEmpty(category))
            url += $"&categories=cs.%7B{Uri.EscapeDataString(category)}%7D";

        if (!string.IsNullOrEmpty(search))
            url += $"&title=ilike.*{Uri.EscapeDataString(search)}*";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Prefer", "count=exact");

        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var contentRange = response.Content.Headers.TryGetValues("Content-Range", out var values)
            ? values.FirstOrDefault() : null;
        var total = ParseTotal(contentRange);

        var json = await response.Content.ReadAsStringAsync();
        var games = JsonSerializer.Deserialize<List<Game>>(json, _json) ?? [];
        return (games, total);
    }

    public async Task<Game?> GetGameBySlugAsync(string slug)
    {
        var url = $"games?select=*&slug=eq.{Uri.EscapeDataString(slug)}&status=eq.done&limit=1";
        var json = await http.GetStringAsync(url);
        var games = JsonSerializer.Deserialize<List<Game>>(json, _json) ?? [];
        return games.FirstOrDefault();
    }

    public async Task<List<Game>> GetSimilarGamesAsync(
        string[] categories, string excludeSlug, int limit = 5)
    {
        if (categories.Length == 0) return [];
        var cats = string.Join(",", categories);
        var url = $"games?select=*&status=eq.done&categories=ov.%7B{Uri.EscapeDataString(cats)}%7D&slug=neq.{Uri.EscapeDataString(excludeSlug)}&limit={limit}";
        var json = await http.GetStringAsync(url);
        return JsonSerializer.Deserialize<List<Game>>(json, _json) ?? [];
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        try
        {
            var json = await http.GetStringAsync("rpc/get_distinct_categories");
            var rows = JsonSerializer.Deserialize<List<CategoryRow>>(json, _json) ?? [];
            return rows.Select(r => r.Category).ToList();
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    private record CategoryRow(string Category);
}
