namespace LinkAnalytics.Api.Models;

public class ShortLink
{
    public const int CodeMaxLength = 16;
    public const int OriginalUrlMaxLength = 2048;

    public int Id { get; set; }

    public required string OriginalUrl { get; set; }

    public required string Code { get; set; }

    /// <summary>Creation date in UTC.</summary>
    public DateTime CreatedAt { get; set; }

    public ICollection<LinkClick> Clicks { get; } = [];
}
