using LinkAnalytics.Api.Data;
using LinkAnalytics.Api.Endpoints;
using LinkAnalytics.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// The connection string is resolved when the DbContext is created (not at startup),
// so tests can override it through configuration.
builder.Services.AddDbContext<LinkAnalyticsDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("LinkAnalytics")
        ?? throw new InvalidOperationException("Connection string 'LinkAnalytics' was not found.");

    options.UseSqlite(connectionString);
});

// Fails at startup (instead of on the first request) when the public base URL is missing or invalid.
builder.Services.AddOptions<ShortLinkOptions>()
    .BindConfiguration(ShortLinkOptions.SectionName)
    .Validate(
        o => ShortLinkOptions.IsValidPublicBaseUrl(o.PublicBaseUrl),
        $"'{ShortLinkOptions.SectionName}:{nameof(ShortLinkOptions.PublicBaseUrl)}' must be an absolute HTTP or HTTPS URL " +
        "without credentials, query string or fragment (e.g. https://links.example.com).")
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IShortCodeGenerator, RandomShortCodeGenerator>();
builder.Services.AddScoped<ShortLinkService>();
builder.Services.AddScoped<LinkStatsService>();

var app = builder.Build();

// HTTP pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // Serves the OpenAPI document at /openapi/v1.json
    app.MapOpenApi();

    // Creates/updates the local SQLite database automatically while developing.
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<LinkAnalyticsDbContext>().Database.Migrate();
}
else
{
    // Tells browsers to use HTTPS only (not enabled in Development to avoid caching it for localhost).
    app.UseHsts();
}

app.UseHttpsRedirection();

// Endpoints
app.MapHealthEndpoints();
app.MapLinkEndpoints();
app.MapRedirectEndpoints();

app.Run();

// Exposes the implicit Program class to the test project (WebApplicationFactory<Program>).
public partial class Program;
