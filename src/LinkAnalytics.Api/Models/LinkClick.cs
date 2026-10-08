namespace LinkAnalytics.Api.Models;

/// <summary>
/// A single access to a short link. Intentionally stores no visitor data
/// (no IP address, user agent, referrer or location).
/// </summary>
public class LinkClick
{
    public long Id { get; set; }

    public int ShortLinkId { get; set; }

    public ShortLink ShortLink { get; set; } = null!;

    /// <summary>Access date and time in UTC.</summary>
    public DateTime ClickedAt { get; set; }
}
