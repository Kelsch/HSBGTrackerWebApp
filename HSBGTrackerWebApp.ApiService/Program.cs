using Dapper;
using HSBGTrackerWebApp.Api.Auth;
using HSBGTrackerWebApp.Api.Data;
using HSBGTrackerWebApp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

SqlMapper.AddTypeHandler(new GuidAsStringHandler());

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

builder.Services.AddControllers();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// The only line that needs to change to move databases - both factories implement the same
var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
if (string.Equals(databaseProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ISqlConnectionFactory, SqlServerConnectionFactory>();
}
else if(string.Equals(databaseProvider, "Postgres", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ISqlConnectionFactory, PostgresConnectionFactory>();
}
else
{
    builder.Services.AddSingleton<ISqlConnectionFactory, SqliteConnectionFactory>();
}

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IGameRepository, GameRepository>();
builder.Services.AddScoped<IOpponentLinkingService, OpponentLinkingService>();

builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
    await DatabaseInitializer.EnsureCreatedAsync(factory);
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

app.Run();
