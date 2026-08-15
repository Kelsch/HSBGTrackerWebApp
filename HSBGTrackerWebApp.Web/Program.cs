using HSBGTrackerWebApp.Web.Components;
using HSBGTrackerWebApp.Web.Services;
using HSBGTrackerWebApp.Web.Services.Cards;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<AuthSessionState>();
builder.Services.AddHttpClient<GamesApiClient>(client =>
{
    var baseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:5001";
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
