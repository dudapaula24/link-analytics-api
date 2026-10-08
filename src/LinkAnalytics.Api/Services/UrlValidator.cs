using System.Diagnostics.CodeAnalysis;
using LinkAnalytics.Api.Models;

namespace LinkAnalytics.Api.Services;

public static class UrlValidator
{
    /// <summary>
    /// Validates that <paramref name="input"/> is an absolute HTTP or HTTPS URL and returns
    /// its normalized form (e.g. "https://Example.com" becomes "https://example.com/").
    /// </summary>
    public static bool TryNormalize(
        string? input,
        [NotNullWhen(true)] out string? normalizedUrl,
        [NotNullWhen(false)] out string? error)
    {
        normalizedUrl = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "The URL is required.";
            return false;
        }

        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host))
        {
            error = "The URL must be an absolute HTTP or HTTPS address.";
            return false;
        }

        // Credentials in the URL ("https://user:pass@host") are a common phishing trick
        // ("https://trusted.example@malicious.example") and should never be stored.
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            error = "The URL must not contain credentials.";
            return false;
        }

        if (uri.AbsoluteUri.Length > ShortLink.OriginalUrlMaxLength)
        {
            error = $"The URL must not exceed {ShortLink.OriginalUrlMaxLength} characters.";
            return false;
        }

        normalizedUrl = uri.AbsoluteUri;
        error = null;
        return true;
    }
}
