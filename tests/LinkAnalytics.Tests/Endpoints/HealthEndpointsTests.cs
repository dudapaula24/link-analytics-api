using System.Net;
using System.Net.Http.Json;
using LinkAnalytics.Api.Endpoints;
using LinkAnalytics.Tests.Infrastructure;

namespace LinkAnalytics.Tests.Endpoints;

public sealed class HealthEndpointsTests : IDisposable
{
    private readonly LinkAnalyticsApiFactory _factory = new();
    private readonly HttpClient _client;

    public HealthEndpointsTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetHealth_ReturnsOkWithHealthyStatus()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("Healthy", body.Status);
    }
}
