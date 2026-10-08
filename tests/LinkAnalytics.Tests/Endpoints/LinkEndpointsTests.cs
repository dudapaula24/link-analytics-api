using System.Net;
using System.Net.Http.Json;
using LinkAnalytics.Api.Contracts;
using LinkAnalytics.Api.Services;
using LinkAnalytics.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LinkAnalytics.Tests.Endpoints;

// xUnit creates a new instance of this class per test, so every test gets its own database.
public sealed class LinkEndpointsTests : IDisposable
{
    private readonly LinkAnalyticsApiFactory _factory = new();
    private readonly HttpClient _client;

    public LinkEndpointsTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task CreateLink_WithValidUrl_Returns201WithLocationAndBody()
    {
        var response = await _client.PostAsJsonAsync("/api/links", new CreateShortLinkRequest("https://example.com/docs?page=1"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var link = await response.Content.ReadFromJsonAsync<ShortLinkResponse>();
        Assert.NotNull(link);
        Assert.True(link.Id > 0);
        Assert.Equal("https://example.com/docs?page=1", link.OriginalUrl);
        Assert.Matches("^[0-9A-Za-z]{7}$", link.Code);
        Assert.Equal($"{LinkAnalyticsApiFactory.DefaultPublicBaseUrl}/r/{link.Code}", link.ShortUrl);
        Assert.Equal(DateTimeKind.Utc, link.CreatedAt.Kind);
        Assert.InRange(link.CreatedAt, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
        Assert.Equal($"/api/links/{link.Code}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task CreateLink_NormalizesUrl()
    {
        var link = await CreateLinkAsync("  HTTP://Example.COM  ");

        Assert.Equal("http://example.com/", link.OriginalUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("example.com")]
    [InlineData("/relative/path")]
    [InlineData("ftp://example.com/file.txt")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("https://user:password@example.com")]
    [InlineData("https://trusted.example@malicious.example/login")]
    public async Task CreateLink_WithInvalidUrl_Returns400ValidationProblem(string? url)
    {
        var response = await _client.PostAsJsonAsync("/api/links", new CreateShortLinkRequest(url));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("url"));
    }

    [Fact]
    public async Task CreateLink_WithTooLongUrl_Returns400()
    {
        var url = "https://example.com/" + new string('a', 2048);

        var response = await _client.PostAsJsonAsync("/api/links", new CreateShortLinkRequest(url));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateLink_WithMalformedJson_Returns400()
    {
        var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/links", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    public async Task CreateLink_WithEmptyOrNullBody_Returns400(string body)
    {
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/links", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateLink_SameUrlTwice_GeneratesDistinctCodes()
    {
        var first = await CreateLinkAsync("https://example.com");
        var second = await CreateLinkAsync("https://example.com");

        Assert.NotEqual(first.Code, second.Code);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task GetLink_WithExistingCode_Returns200WithLink()
    {
        var created = await CreateLinkAsync("https://example.com/article");

        var response = await _client.GetAsync($"/api/links/{created.Code}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var link = await response.Content.ReadFromJsonAsync<ShortLinkResponse>();
        Assert.Equal(created, link);
    }

    [Fact]
    public async Task GetLink_WithUnknownCode_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/links/unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetLink_CodeLookupIsCaseSensitive()
    {
        using var factory = new LinkAnalyticsApiFactory(services =>
            services.Replace(ServiceDescriptor.Singleton<IShortCodeGenerator>(new SequenceShortCodeGenerator("AbC1234"))));
        using var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/links", new CreateShortLinkRequest("https://example.com"))).EnsureSuccessStatusCode();

        var exactCase = await client.GetAsync("/api/links/AbC1234");
        var otherCase = await client.GetAsync("/api/links/abc1234");

        Assert.Equal(HttpStatusCode.OK, exactCase.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherCase.StatusCode);
    }

    [Fact]
    public async Task Redirect_WithExistingCode_Returns302ToOriginalUrl()
    {
        var created = await CreateLinkAsync("https://example.com/target?x=1");
        using var client = _factory.CreateNonRedirectingClient();

        var response = await client.GetAsync($"/r/{created.Code}");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(new Uri("https://example.com/target?x=1"), response.Headers.Location);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Redirect_WithUnknownCode_Returns404()
    {
        using var client = _factory.CreateNonRedirectingClient();

        var response = await client.GetAsync("/r/unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task CreateLink_WhenGeneratedCodeAlreadyExists_RetriesWithNewCode()
    {
        // The first two generated codes collide; the third one is free.
        var generator = new SequenceShortCodeGenerator("dup0001", "dup0001", "new0002");
        using var factory = new LinkAnalyticsApiFactory(services =>
            services.Replace(ServiceDescriptor.Singleton<IShortCodeGenerator>(generator)));
        using var client = factory.CreateClient();

        var first = await (await client.PostAsJsonAsync("/api/links", new CreateShortLinkRequest("https://a.example.com")))
            .Content.ReadFromJsonAsync<ShortLinkResponse>();
        var secondResponse = await client.PostAsJsonAsync("/api/links", new CreateShortLinkRequest("https://b.example.com"));
        var second = await secondResponse.Content.ReadFromJsonAsync<ShortLinkResponse>();

        Assert.Equal("dup0001", first?.Code);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Equal("new0002", second?.Code);
    }

    [Fact]
    public async Task Database_IsIsolatedPerFactory()
    {
        var created = await CreateLinkAsync("https://example.com");
        using var otherFactory = new LinkAnalyticsApiFactory();
        using var otherClient = otherFactory.CreateClient();

        var response = await otherClient.GetAsync($"/api/links/{created.Code}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<ShortLinkResponse> CreateLinkAsync(string url)
    {
        var response = await _client.PostAsJsonAsync("/api/links", new CreateShortLinkRequest(url));
        response.EnsureSuccessStatusCode();

        var link = await response.Content.ReadFromJsonAsync<ShortLinkResponse>();
        Assert.NotNull(link);
        return link;
    }

    private sealed class SequenceShortCodeGenerator(params string[] codes) : IShortCodeGenerator
    {
        private readonly Queue<string> _codes = new(codes);

        public string Generate() => _codes.Dequeue();
    }
}
