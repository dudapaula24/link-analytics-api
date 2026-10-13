namespace LinkAnalytics.Api.Contracts;

public sealed record CreateShortLinkRequest(string? Url);

public sealed record ShortLinkResponse(
    int Id,
    string Code,
    string OriginalUrl,
    string ShortUrl,
    DateTime CreatedAt);
