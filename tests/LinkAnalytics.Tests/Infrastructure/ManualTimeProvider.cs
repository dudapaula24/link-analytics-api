namespace LinkAnalytics.Tests.Infrastructure;

/// <summary>A clock that only changes when the test sets it.</summary>
public sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
