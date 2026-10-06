using DailyMachineSpirit.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DailyMachineSpirit.Data;

/// <summary>
/// The production database is shared with another app, so everything here, the migrations history included, lives in
/// its own schema and never in <c>dbo</c>.
/// </summary>
public class MachineSpiritDbContext : DbContext
{
    public const string Schema = "machinespirit";
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";

    public MachineSpiritDbContext(DbContextOptions<MachineSpiritDbContext> options) : base(options) { }

    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemProfile> ItemProfiles => Set<ItemProfile>();

    public static void ConfigureSqlServer(Microsoft.EntityFrameworkCore.Infrastructure.SqlServerDbContextOptionsBuilder sql)
        => sql.MigrationsHistoryTable(MigrationsHistoryTable, Schema).EnableRetryOnFailure();

    // datetime2 has no time zone, so a value read back would be DateTimeKind.Unspecified and lose its "Z" in JSON.
    // Every DateTime here is UTC: stored as UTC, read back marked as UTC.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasKey(e => e.Id);
            // Two instances or a retried timer must not publish twice in a day.
            entity.HasIndex(e => e.PublishedOnUtc).IsUnique();
            entity.Property(e => e.Kind).HasConversion<string>().HasMaxLength(10);
            entity.Property(e => e.Title).HasMaxLength(Item.MaxTitleLength).IsRequired();
            entity.Property(e => e.Text).HasMaxLength(Item.MaxTextLength).IsRequired();
            entity.Property(e => e.HereticalTruth).HasMaxLength(Item.MaxHereticalTruthLength).IsRequired();
            entity.Property(e => e.GeneratedByModel).HasMaxLength(Item.MaxModelLength).IsRequired();
        });

        modelBuilder.Entity<ItemProfile>(entity =>
        {
            entity.HasKey(e => e.ItemId);
            entity.Property(e => e.ScoresGeneratorVersion).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Scores)
                .HasConversion<ScoresToBytesConverter, ScoresComparer>()
                .HasMaxLength(8000)
                .IsRequired();
            entity.HasOne(e => e.Item)
                .WithOne()
                .HasForeignKey<ItemProfile>(e => e.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
