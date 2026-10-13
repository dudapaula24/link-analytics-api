using LinkAnalytics.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LinkAnalytics.Tests.Infrastructure;

/// <summary>
/// Hosts the API in memory backed by its own private SQLite in-memory database.
/// Each factory instance gets a uniquely named database, so tests never share data.
/// </summary>
public sealed class LinkAnalyticsApiFactory : WebApplicationFactory<Program>
{
    public const string DefaultPublicBaseUrl = "https://links.example.com";

    private readonly Action<IServiceCollection>? _configureServices;
    private readonly string? _publicBaseUrl;

    private readonly string _connectionString =
        $"Data Source=link-analytics-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False";

    // A shared-cache in-memory database lives only while at least one connection is open.
    private SqliteConnection? _keepAliveConnection;

    public LinkAnalyticsApiFactory(
        Action<IServiceCollection>? configureServices = null,
        string? publicBaseUrl = DefaultPublicBaseUrl)
    {
        _configureServices = configureServices;
        _publicBaseUrl = publicBaseUrl;

        // Requests are sent over HTTPS, like in production, so UseHttpsRedirection has nothing to do.
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    /// <summary>Creates a client that exposes redirect responses instead of following them.</summary>
    public HttpClient CreateNonRedirectingClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = ClientOptions.BaseAddress,
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:LinkAnalytics", _connectionString);
        builder.UseSetting("ShortLinks:PublicBaseUrl", _publicBaseUrl);

        if (_configureServices is not null)
        {
            builder.ConfigureTestServices(_configureServices);
        }
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();

        var host = base.CreateHost(builder);

        // Apply the real migrations so the tests also validate the database schema.
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<LinkAnalyticsDbContext>().Database.Migrate();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _keepAliveConnection?.Dispose();
        }
    }
}
