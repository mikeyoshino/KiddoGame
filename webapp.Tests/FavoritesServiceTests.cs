using Kiddo.Web.Services;
using Microsoft.JSInterop;

namespace Kiddo.Web.Tests;

public class FavoritesServiceTests
{
    [Fact]
    public async Task LoadAsync_ParsesSlugsFromStorage()
    {
        var js = new FakeJSRuntime();
        js.Store["kiddo_favorites"] = """["slug-a","slug-b"]""";
        var svc = new FavoritesService(js);

        await svc.LoadAsync();

        Assert.True(svc.IsFavorite("slug-a"));
        Assert.True(svc.IsFavorite("slug-b"));
        Assert.False(svc.IsFavorite("slug-c"));
    }

    [Fact]
    public async Task LoadAsync_HandlesEmptyStorage()
    {
        var js = new FakeJSRuntime();
        var svc = new FavoritesService(js);

        await svc.LoadAsync();

        Assert.False(svc.IsFavorite("any-slug"));
    }

    [Fact]
    public async Task LoadAsync_IsNoOpOnSecondCall()
    {
        var js = new FakeJSRuntime();
        var svc = new FavoritesService(js);

        await svc.LoadAsync();
        js.Store["kiddo_favorites"] = """["slug-a"]""";
        await svc.LoadAsync(); // should not re-read

        Assert.False(svc.IsFavorite("slug-a")); // first load was empty
    }

    [Fact]
    public async Task ToggleAsync_AddsFavorite()
    {
        var js = new FakeJSRuntime();
        var svc = new FavoritesService(js);
        await svc.LoadAsync();

        await svc.ToggleAsync("slug-a");

        Assert.True(svc.IsFavorite("slug-a"));
        Assert.Contains("slug-a", js.Store["kiddo_favorites"]);
    }

    [Fact]
    public async Task ToggleAsync_RemovesExistingFavorite()
    {
        var js = new FakeJSRuntime();
        js.Store["kiddo_favorites"] = """["slug-a"]""";
        var svc = new FavoritesService(js);
        await svc.LoadAsync();

        await svc.ToggleAsync("slug-a");

        Assert.False(svc.IsFavorite("slug-a"));
    }

    [Fact]
    public async Task ToggleAsync_FiresOnChanged()
    {
        var js = new FakeJSRuntime();
        var svc = new FavoritesService(js);
        await svc.LoadAsync();
        var fired = false;
        svc.OnChanged += () => fired = true;

        await svc.ToggleAsync("slug-a");

        Assert.True(fired);
    }

    [Fact]
    public async Task GetSlugs_ReturnsCurrentSlugs()
    {
        var js = new FakeJSRuntime();
        js.Store["kiddo_favorites"] = """["slug-a","slug-b"]""";
        var svc = new FavoritesService(js);
        await svc.LoadAsync();

        var slugs = svc.GetSlugs();

        Assert.Contains("slug-a", slugs);
        Assert.Contains("slug-b", slugs);
    }
}

internal class FakeJSRuntime : IJSRuntime
{
    public Dictionary<string, string> Store { get; } = new();

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (identifier == "localStorage.getItem" && args is [var k, ..])
        {
            Store.TryGetValue(k?.ToString() ?? "", out var val);
            return ValueTask.FromResult((TValue)(object?)val!);
        }
        if (identifier == "localStorage.setItem" && args is [var key, var value, ..])
        {
            Store[key?.ToString() ?? ""] = value?.ToString() ?? "";
            return ValueTask.FromResult(default(TValue)!);
        }
        return ValueTask.FromResult(default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);
}
