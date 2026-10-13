namespace LinkAnalytics.Api.Contracts;

public sealed record LinkStatsResponse(
    string Code,
    int TotalClicks,
    DateTime? LastClickAt,
    IReadOnlyList<DailyClicksResponse> ClicksByDay);

/// <param name="Date">Day in UTC (yyyy-MM-dd).</param>
public sealed record DailyClicksResponse(DateOnly Date, int Clicks);
