using LinkAnalytics.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LinkAnalytics.Api.Data;

public class LinkAnalyticsDbContext(DbContextOptions<LinkAnalyticsDbContext> options) : DbContext(options)
{
    public DbSet<ShortLink> ShortLinks => Set<ShortLink>();

    public DbSet<LinkClick> LinkClicks => Set<LinkClick>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no native date type, so values come back with Kind = Unspecified.
        // All dates are stored in UTC, so mark them as UTC to serialize them with the "Z" suffix.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShortLink>(entity =>
        {
            entity.HasKey(l => l.Id);

            entity.Property(l => l.OriginalUrl)
                .HasMaxLength(ShortLink.OriginalUrlMaxLength)
                .IsRequired();

            entity.Property(l => l.Code)
                .HasMaxLength(ShortLink.CodeMaxLength)
                .IsRequired();

            // The database is the final guarantee that codes are unique.
            entity.HasIndex(l => l.Code).IsUnique();
        });

        modelBuilder.Entity<LinkClick>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.HasOne(c => c.ShortLink)
                .WithMany(l => l.Clicks)
                .HasForeignKey(c => c.ShortLinkId)
                .OnDelete(DeleteBehavior.Cascade);

            // Covers the stats queries: filter by link, then count/group/max by date.
            entity.HasIndex(c => new { c.ShortLinkId, c.ClickedAt });
        });
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v,
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
