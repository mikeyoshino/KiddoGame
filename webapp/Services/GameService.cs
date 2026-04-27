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

    internal static string BuildGamesUrl(
        int page, int pageSize, IReadOnlySet<string>? categories, string? search, Lang lang = Lang.English)
    {
        var offset = (page - 1) * pageSize;
        var url = $"games?select=*&order=created_at.desc&offset={offset}&limit={pageSize}";

        if (categories is { Count: > 0 })
        {
            var cats = string.Join(",", categories);
            url += $"&categories=ov.%7B{Uri.EscapeDataString(cats)}%7D";
        }

        if (!string.IsNullOrEmpty(search))
        {
            var term = Uri.EscapeDataString(search);
            var descCol = lang == Lang.Thai ? "description_th" : "description";
            url += $"&or=(title.ilike.*{term}*,{descCol}.ilike.*{term}*)";
        }

        return url;
    }

    public async Task<(List<Game> Games, int Total)> GetGamesAsync(
        int page, int pageSize = 30, IReadOnlySet<string>? categories = null, string? search = null, Lang lang = Lang.English)
    {
        var url = BuildGamesUrl(page, pageSize, categories, search, lang);

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
        var url = $"games?select=*&slug=eq.{Uri.EscapeDataString(slug)}&limit=1";
        var json = await http.GetStringAsync(url);
        var games = JsonSerializer.Deserialize<List<Game>>(json, _json) ?? [];
        return games.FirstOrDefault();
    }

    public async Task<List<Game>> GetSimilarGamesAsync(
        string[] categories, string excludeSlug, int limit = 5)
    {
        if (categories.Length == 0) return [];
        var cats = string.Join(",", categories);
        var url = $"games?select=*&categories=ov.%7B{Uri.EscapeDataString(cats)}%7D&slug=neq.{Uri.EscapeDataString(excludeSlug)}&limit={limit}";
        var json = await http.GetStringAsync(url);
        return JsonSerializer.Deserialize<List<Game>>(json, _json) ?? [];
    }

    public async Task<List<Game>> GetGamesBySlugsAsync(string[] slugs)
    {
        if (slugs.Length == 0) return [];
        var inList = string.Join(",", slugs.Select(Uri.EscapeDataString));
        var url = $"games?select=*&slug=in.({inList})";
        var json = await http.GetStringAsync(url);
        return JsonSerializer.Deserialize<List<Game>>(json, _json) ?? [];
    }

    public async Task<List<(string Category, int Count)>> GetCategoriesAsync()
    {
        try
        {
            var json = await http.GetStringAsync("rpc/get_distinct_categories");
            var rows = JsonSerializer.Deserialize<List<CategoryRow>>(json, _json) ?? [];
            return rows.Select(r => (r.Category, r.Count)).ToList();
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<int> GetTotalCountAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Head, "games?select=*&status=eq.done");
        request.Headers.TryAddWithoutValidation("Prefer", "count=exact");
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var contentRange = response.Content.Headers.TryGetValues("Content-Range", out var values)
            ? values.FirstOrDefault() : null;
        return ParseTotal(contentRange);
    }

    public async Task<List<string>> GetAllSlugsAsync()
    {
        var json = await http.GetStringAsync("games?select=slug&status=eq.done&limit=10000");
        var rows = JsonSerializer.Deserialize<List<SlugRow>>(json, _json) ?? [];
        return rows.Select(r => r.Slug).ToList();
    }

    private record CategoryRow(string Category, int Count);
    private record SlugRow(string Slug);
}
