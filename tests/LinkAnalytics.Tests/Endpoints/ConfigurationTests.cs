using System.Net;
using System.Net.Http.Json;
using LinkAnalytics.Api.Contracts;
using LinkAnalytics.Tests.Infrastructure;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LinkAnalytics.Tests.Endpoints;

public class ConfigurationTests
{
    [Theory]
    [InlineData("https://links.example.com", "https://links.example.com/r/")]
    [InlineData("https://links.example.com/", "https://links.example.com/r/")]
    [InlineData("https://example.com/go", "https://example.com/go/r/")]
    [InlineData("http://localhost:5087", "http://localhost:5087/r/")]
    public async Task ShortUrl_IsBuiltFromConfiguredPublicBaseUrl(string publicBaseUrl, string expectedPrefix)
    {
        using var factory = new LinkAnalyticsApiFactory(publicBaseUrl: publicBaseUrl);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/links", new CreateShortLinkRequest("https://example.org"));
        var link = await response.Content.ReadFromJsonAsync<ShortLinkResponse>();

        Assert.NotNull(link);
        Assert.Equal(expectedPrefix + link.Code, link.ShortUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("/relative")]
    [InlineData("ftp://links.example.com")]
    [InlineData("https://links.example.com?ref=1")]
    [InlineData("https://links.example.com#top")]
    [InlineData("https://user:secret@links.example.com")]
    public void Startup_WithInvalidPublicBaseUrl_Fails(string? publicBaseUrl)
    {
        using var factory = new LinkAnalyticsApiFactory(publicBaseUrl: publicBaseUrl);

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("PublicBaseUrl", exception.Message);
    }

    [Fact]
    public async Task Request_WithHostNotInAllowedHosts_IsRejected()
    {
        using var factory = new LinkAnalyticsApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Host = "malicious.example";

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Request_OverHttp_IsRedirectedToHttps()
    {
        // The in-memory test server has no HTTPS endpoint, so the port Kestrel would report is set explicitly.
        using var factory = new LinkAnalyticsApiFactory(services =>
            services.Configure<HttpsRedirectionOptions>(o => o.HttpsPort = 443));
        using var client = factory.CreateNonRedirectingClient();

        var response = await client.GetAsync("http://localhost/health");

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal(Uri.UriSchemeHttps, response.Headers.Location?.Scheme);
    }
}
