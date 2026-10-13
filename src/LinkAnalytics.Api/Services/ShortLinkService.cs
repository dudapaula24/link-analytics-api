using LinkAnalytics.Api.Data;
using LinkAnalytics.Api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LinkAnalytics.Api.Services;

public sealed class ShortLinkService(
    LinkAnalyticsDbContext db,
    IShortCodeGenerator codeGenerator,
    TimeProvider timeProvider)
{
    internal const int MaxCodeGenerationAttempts = 5;

    // SQLITE_CONSTRAINT_UNIQUE: raised when the unique index on Code is violated.
    // Other constraint errors (NOT NULL, FOREIGN KEY...) are real failures and must not be retried.
    private const int SqliteConstraintUniqueErrorCode = 2067;

    /// <summary>Creates a short link for an already validated and normalized URL.</summary>
    public async Task<ShortLink> CreateAsync(string originalUrl, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxCodeGenerationAttempts; attempt++)
        {
            var code = codeGenerator.Generate();

            // Cheap pre-check that avoids most collisions.
            if (await db.ShortLinks.AnyAsync(l => l.Code == code, cancellationToken))
            {
                continue;
            }

            var link = new ShortLink
            {
                OriginalUrl = originalUrl,
                Code = code,
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            };

            db.ShortLinks.Add(link);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return link;
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteConstraintUniqueErrorCode })
            {
                // Another request inserted the same code between the check and the insert.
                db.Entry(link).State = EntityState.Detached;
            }
        }

        throw new InvalidOperationException(
            $"Could not generate a unique short code after {MaxCodeGenerationAttempts} attempts.");
    }

    public Task<ShortLink?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
        db.ShortLinks
            .AsNoTracking()
            .SingleOrDefaultAsync(l => l.Code == code, cancellationToken);
}
