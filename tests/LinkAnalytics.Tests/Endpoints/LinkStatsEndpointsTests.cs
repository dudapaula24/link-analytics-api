using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinkAnalytics.Api.Contracts;
using LinkAnalytics.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LinkAnalytics.Tests.Endpoints;

// xUnit creates a new instance of this class per test, so every test gets its own database.
public sealed class LinkStatsEndpointsTests : IDisposable
{
    private static readonly DateTimeOffset StartTime = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly ManualTimeProvider _clock = new(StartTime);
    private readonly LinkAnalyticsApiFactory _factory;
    private readonly HttpClient _client;

    public LinkStatsEndpointsTests()
    {
        _factory = new LinkAnalyticsApiFactory(services =>
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(_clock)));
        _client = _factory.CreateNonRedirectingClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Stats_ForLinkWithoutClicks_ReturnsZeroedStats()
    {
        var link = await CreateLinkAsync();

        var stats = await GetStatsAsync(link.Code);

        Assert.Equal(link.Code, stats.Code);
        Assert.Equal(0, stats.TotalClicks);
        Assert.Null(stats.LastClickAt);
        Assert.Empty(stats.ClicksByDay);
    }

    [Fact]
    public async Task Stats_WithUnknownCode_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/links/unknown/stats");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Redirect_RecordsClickWithUtcTimestamp()
    {
        var link = await CreateLinkAsync();

        var redirect = await _client.GetAsync($"/r/{link.Code}");
        var stats = await GetStatsAsync(link.Code);

        Assert.Equal(HttpStatusCode.Found, redirect.StatusCode);
        Assert.Equal(1, stats.TotalClicks);
        Assert.Equal(StartTime.UtcDateTime, stats.LastClickAt);
        Assert.Equal(DateTimeKind.Utc, stats.LastClickAt!.Value.Kind);
        Assert.Equal([new DailyClicksResponse(new DateOnly(2026, 10, 1), 1)], stats.ClicksByDay);
    }

    [Fact]
    public async Task Redirect_MultipleAccesses_AreAllCounted()
    {
        var link = await CreateLinkAsync();

        for (var i = 0; i < 3; i++)
        {
            _clock.UtcNow = StartTime.AddMinutes(i);
            await RedirectAsync(link.Code);
        }

        var stats = await GetStatsAsync(link.Code);

        Assert.Equal(3, stats.TotalClicks);
        Assert.Equal(StartTime.AddMinutes(2).UtcDateTime, stats.LastClickAt);
    }

    [Fact]
    public async Task Stats_GroupsClicksByUtcDay_InChronologicalOrder()
    {
        var link = await CreateLinkAsync();

        // Recorded out of order on purpose; the response must still be sorted by day.
        await RedirectAtAsync(link.Code, new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero));
        await RedirectAtAsync(link.Code, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        await RedirectAtAsync(link.Code, new DateTimeOffset(2026, 10, 1, 12, 30, 0, TimeSpan.Zero));
        await RedirectAtAsync(link.Code, new DateTimeOffset(2026, 10, 1, 23, 59, 59, TimeSpan.Zero));
        await RedirectAtAsync(link.Code, new DateTimeOffset(2026, 10, 2, 0, 0, 1, TimeSpan.Zero));

        var stats = await GetStatsAsync(link.Code);

        Assert.Equal(5, stats.TotalClicks);
        Assert.Equal(new DateTime(2026, 10, 4, 8, 0, 0, DateTimeKind.Utc), stats.LastClickAt);
        Assert.Equal(
            [
                new DailyClicksResponse(new DateOnly(2026, 10, 1), 3),
                new DailyClicksResponse(new DateOnly(2026, 10, 2), 1),
                new DailyClicksResponse(new DateOnly(2026, 10, 4), 1),
            ],
            stats.ClicksByDay);
    }

    [Fact]
    public async Task Stats_OnlyCountClicksOfTheRequestedLink()
    {
        var first = await CreateLinkAsync();
        var second = await CreateLinkAsync();

        await RedirectAsync(first.Code);
        await RedirectAsync(first.Code);
        await RedirectAsync(second.Code);

        Assert.Equal(2, (await GetStatsAsync(first.Code)).TotalClicks);
        Assert.Equal(1, (await GetStatsAsync(second.Code)).TotalClicks);
    }

    [Fact]
    public async Task Stats_NotAffectedByDetailsLookupOrUnknownCodeRedirect()
    {
        var link = await CreateLinkAsync();

        await _client.GetAsync($"/api/links/{link.Code}");
        var unknownRedirect = await _client.GetAsync("/r/unknown");

        Assert.Equal(HttpStatusCode.NotFound, unknownRedirect.StatusCode);
        Assert.Equal(0, (await GetStatsAsync(link.Code)).TotalClicks);
    }

    [Fact]
    public async Task Stats_SerializesDaysAsIsoDates()
    {
        var link = await CreateLinkAsync();
        await RedirectAsync(link.Code);

        using var json = JsonDocument.Parse(await _client.GetStringAsync($"/api/links/{link.Code}/stats"));
        var root = json.RootElement;

        Assert.Equal("2026-10-01T10:00:00Z", root.GetProperty("lastClickAt").GetString());
        Assert.Equal("2026-10-01", root.GetProperty("clicksByDay")[0].GetProperty("date").GetString());
        Assert.Equal(1, root.GetProperty("clicksByDay")[0].GetProperty("clicks").GetInt32());
    }

    private async Task<ShortLinkResponse> CreateLinkAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/links", new CreateShortLinkRequest("https://example.com"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ShortLinkResponse>())!;
    }

    private async Task RedirectAsync(string code)
    {
        var response = await _client.GetAsync($"/r/{code}");
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    private async Task RedirectAtAsync(string code, DateTimeOffset utcNow)
    {
        _clock.UtcNow = utcNow;
        await RedirectAsync(code);
    }

    private async Task<LinkStatsResponse> GetStatsAsync(string code)
    {
        var response = await _client.GetAsync($"/api/links/{code}/stats");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LinkStatsResponse>())!;
    }
}
