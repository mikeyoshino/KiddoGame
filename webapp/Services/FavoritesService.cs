using System.Text.Json;
using Microsoft.JSInterop;

namespace Kiddo.Web.Services;

public class FavoritesService(IJSRuntime js)
{
    private const string Key = "kiddo_favorites";
    private HashSet<string> _slugs = [];
    private bool _loaded;

    public event Action? OnChanged;

    public async Task LoadAsync()
    {
        if (_loaded) return;
        var json = await js.InvokeAsync<string?>("localStorage.getItem", Key);
        if (!string.IsNullOrEmpty(json))
        {
            var slugs = JsonSerializer.Deserialize<string[]>(json) ?? [];
            _slugs = new HashSet<string>(slugs);
        }
        _loaded = true;
    }

    public async Task ToggleAsync(string slug)
    {
        if (_slugs.Contains(slug))
            _slugs.Remove(slug);
        else
            _slugs.Add(slug);

        await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(_slugs));
        OnChanged?.Invoke();
    }

    public bool IsFavorite(string slug) => _slugs.Contains(slug);

    public string[] GetSlugs() => [.. _slugs];
}
