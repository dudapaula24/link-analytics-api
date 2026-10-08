namespace LinkAnalytics.Api.Services;

public sealed class ShortLinkOptions
{
    public const string SectionName = "ShortLinks";

    /// <summary>
    /// Public address used to build short URLs, e.g. "https://links.example.com".
    /// May include a path (e.g. "https://example.com/go"). Short URLs are built from this value
    /// instead of the request Host header, which can be spoofed.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    public string BuildShortUrl(string code) =>
        $"{PublicBaseUrl.TrimEnd('/')}/r/{Uri.EscapeDataString(code)}";

    public static bool IsValidPublicBaseUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host)
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);
}
