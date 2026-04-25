using Kiddo.Web.Components;
using Kiddo.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<LanguageService>();
builder.Services.AddScoped<FavoritesService>();

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
