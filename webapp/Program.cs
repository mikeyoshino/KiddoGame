using Kiddo.Web.Components;
using Kiddo.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<LanguageService>();
builder.Services.AddScoped<FavoritesService>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddHttpClient("thumbnail", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:150.0) Gecko/20100101 Firefox/150.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient("supabase-ingest");
builder.Services.AddHttpClient("openai");
builder.Services.AddScoped<IngestService>();

builder.Services.AddHttpClient<GameService>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var url = config["Supabase:Url"]!;
    var key = config["Supabase:Key"]!;
    client.BaseAddress = new Uri($"{url}/rest/v1/");
    client.DefaultRequestHeaders.Add("apikey", key);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapPost("/api/ingest/filter-new", async (string[] ids, IngestService ingestSvc) =>
{
    var newIds = await ingestSvc.FilterNewAsync(ids);
    return Results.Ok(newIds);
});

app.MapPost("/api/ingest/batch", async (
    Kiddo.Web.Models.IngestBatchRequest req, IngestService ingestSvc) =>
{
    var games = req.Games;

    var thumbUrls = await Task.WhenAll(
        games.Select(g => ingestSvc.DownloadThumbnailAsync(g.ObjectId, g.ThumbnailUrl)));

    var thumbnails = games
        .Select((g, i) => (g.ObjectId, Url: thumbUrls[i]))
        .ToDictionary(x => x.ObjectId, x => x.Url);

    var translations = await ingestSvc.TranslateBatchAsync(games);

    await ingestSvc.UpsertGamesAsync(games, thumbnails, translations);

    var results = games.Select(g =>
    {
        var thumbOk = thumbnails.TryGetValue(g.ObjectId, out var url) && url != null;
        return thumbOk
            ? new Kiddo.Web.Models.IngestResult(g.ObjectId, true)
            : new Kiddo.Web.Models.IngestResult(g.ObjectId, false, "thumbnail: all extensions failed");
    }).ToArray();

    return Results.Ok(new Kiddo.Web.Models.IngestBatchResponse(results));
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/sitemap.xml", async (GameService gameSvc) =>
{
    List<string> slugs;
    try { slugs = await gameSvc.GetAllSlugsAsync(); }
    catch { return Results.StatusCode(503); }

    var sb = new System.Text.StringBuilder();
    sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
    sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

    sb.AppendLine("  <url>");
    sb.AppendLine("    <loc>https://kiddogame.net/</loc>");
    sb.AppendLine("    <changefreq>daily</changefreq>");
    sb.AppendLine("    <priority>1.0</priority>");
    sb.AppendLine("  </url>");

    sb.AppendLine("  <url>");
    sb.AppendLine("    <loc>https://kiddogame.net/about</loc>");
    sb.AppendLine("    <changefreq>monthly</changefreq>");
    sb.AppendLine("    <priority>0.9</priority>");
    sb.AppendLine("  </url>");

    foreach (var slug in slugs)
    {
        sb.AppendLine("  <url>");
        sb.AppendLine($"    <loc>https://kiddogame.net/games/{slug}</loc>");
        sb.AppendLine("    <changefreq>monthly</changefreq>");
        sb.AppendLine("    <priority>0.8</priority>");
        sb.AppendLine("  </url>");
    }

    sb.AppendLine("</urlset>");
    return Results.Content(sb.ToString(), "application/xml");
});

app.Run();
