using LinkAnalytics.Api.Contracts;
using LinkAnalytics.Api.Data;
using LinkAnalytics.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LinkAnalytics.Api.Services;

public sealed class LinkStatsService(LinkAnalyticsDbContext db, TimeProvider timeProvider)
{
    public async Task RecordClickAsync(int shortLinkId, CancellationToken cancellationToken)
    {
        db.LinkClicks.Add(new LinkClick
        {
            ShortLinkId = shortLinkId,
            ClickedAt = timeProvider.GetUtcNow().UtcDateTime,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Returns the stats for a link, or <c>null</c> when the code does not exist.</summary>
    public async Task<LinkStatsResponse?> GetStatsAsync(string code, CancellationToken cancellationToken)
    {
        var link = await db.ShortLinks
            .Where(l => l.Code == code)
            .Select(l => new { l.Id, l.Code })
            .SingleOrDefaultAsync(cancellationToken);

        if (link is null)
        {
            return null;
        }

        var clicks = db.LinkClicks.Where(c => c.ShortLinkId == link.Id);

        // Aggregations run in the database; only one row per day is loaded into memory.
        var clicksByDay = await clicks
            .GroupBy(c => c.ClickedAt.Date)
            .Select(g => new { Day = g.Key, Clicks = g.Count() })
            .OrderBy(d => d.Day)
            .ToListAsync(cancellationToken);

        var lastClickAt = await clicks.MaxAsync(c => (DateTime?)c.ClickedAt, cancellationToken);

        return new LinkStatsResponse(
            link.Code,
            clicksByDay.Sum(d => d.Clicks),
            lastClickAt,
            clicksByDay.Select(d => new DailyClicksResponse(DateOnly.FromDateTime(d.Day), d.Clicks)).ToList());
    }
}
