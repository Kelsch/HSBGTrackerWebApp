using HSBGTrackerWebApp.Web.Components;
using HSBGTrackerWebApp.Web.Services;
using HSBGTrackerWebApp.Web.Services.Cards;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

builder.Services.AddScoped<AuthSessionState>();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                     ".aspnet", "DataProtection-Keys")))
    .SetApplicationName("HSBGTrackerWeb");

builder.Services.AddHttpClient<GamesApiClient>(client =>
{
    var baseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7333";
    client.BaseAddress = new Uri(baseUrl);
});

var hsbgBaseUrl = builder.Configuration["HsbgCards:BaseUrl"] ?? "https://hsbg.cards";
builder.Services.AddHttpClient(HsbgCardsClient.HttpClientName, client =>
{
    client.BaseAddress = new Uri(hsbgBaseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("HSBGTrackerWeb/1.0");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddSingleton<HsbgCardsClient>();
builder.Services.AddSingleton<ICardResolver, CardResolver>();
builder.Services.AddHostedService<HsbgCardsRefreshService>();

var app = builder.Build();

if (app.Environment.IsDevelopment() == false)
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

if (app.Environment.IsEnvironment("Docker") == false)
{
    app.UseHttpsRedirection();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapStaticAssets();
app.UseOutputCache();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
