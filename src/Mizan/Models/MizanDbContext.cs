using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mizan.Models;

public class MizanDbContext(DbContextOptions<MizanDbContext> options) : DbContext(options)
{
    // SQLite's 'now' modifier is UTC. Applied as each table's default for created_at/updated_at
    // on insert; updated_at additionally gets bumped by an AFTER UPDATE trigger per table (see
    // the InitialSchema migration) since SQLite has no ON UPDATE CURRENT_TIMESTAMP column clause.
    private const string UtcNowSql = "strftime('%Y-%m-%dT%H:%M:%fZ','now')";

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Txn> Txns => Set<Txn>();
    public DbSet<TxnVoid> TxnVoids => Set<TxnVoid>();
    public DbSet<TxnSupersession> TxnSupersessions => Set<TxnSupersession>();
    public DbSet<TxnSplit> TxnSplits => Set<TxnSplit>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<HoldingTxn> HoldingTxns => Set<HoldingTxn>();
    public DbSet<Price> Prices => Set<Price>();
    public DbSet<Period> Periods => Set<Period>();
    public DbSet<Snapshot> Snapshots => Set<Snapshot>();
    public DbSet<SnapshotLine> SnapshotLines => Set<SnapshotLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("account", t =>
            {
                t.HasCheckConstraint(
                    "CK_account_type",
                    "type IN ('cash','bank','card','broker','loan','physical_asset','receivable','other')");
                t.HasCheckConstraint(
                    "CK_account_liquidity_class",
                    "liquidity_class IN ('immediate','short_term','illiquid','debt')");
            });

            e.Property(a => a.Type).HasConversion(SnakeCaseEnumConverter.For<AccountType>());
            e.Property(a => a.LiquidityClass).HasConversion(SnakeCaseEnumConverter.For<LiquidityClass>());

            // Case-insensitive: "Bank - Current" and "bank - current" are the same account name.
            e.Property(a => a.Name).UseCollation("NOCASE");
            e.HasIndex(a => a.Name).IsUnique();

            ConfigureTimestamps(e);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("category", t => t.HasCheckConstraint(
                "CK_category_kind",
                "kind IN ('income','expense','transfer','investment','adjustment')"));

            e.Property(c => c.Kind).HasConversion(SnakeCaseEnumConverter.For<CategoryKind>());

            e.HasOne(c => c.Parent)
                .WithMany()
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            ConfigureTimestamps(e);
        });

        modelBuilder.Entity<Txn>(e =>
        {
            e.ToTable("txn", t => t.HasCheckConstraint(
                "CK_txn_origin",
                "origin IN ('import','manual','split','adjustment','seed')"));

            e.Property(x => x.Origin).HasConversion(SnakeCaseEnumConverter.For<TxnOrigin>());

            // Unique across every txn ever, void or superseded included — not just "live" rows.
            // occurrence_index already exists to distinguish genuinely-repeated real
            // transactions, so no live row should ever need to reuse a dead row's exact key.
            e.HasIndex(x => x.DedupeKey).IsUnique();

            e.HasOne(x => x.Account)
                .WithMany(a => a.Txns)
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            ConfigureTimestamps(e);
        });

        modelBuilder.Entity<TxnVoid>(e =>
        {
            e.ToTable("txn_void");

            e.HasOne(v => v.Txn)
                .WithMany()
                .HasForeignKey(v => v.TxnId)
                .OnDelete(DeleteBehavior.Restrict);

            ConfigureTimestamps(e);
        });

        modelBuilder.Entity<TxnSupersession>(e =>
        {
            e.ToTable("txn_supersession");

            e.HasOne(s => s.OldTxn)
                .WithMany()
                .HasForeignKey(s => s.OldTxnId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.NewTxn)
                .WithMany()
                .HasForeignKey(s => s.NewTxnId)
                .OnDelete(DeleteBehavior.Restrict);

            ConfigureTimestamps(e);
        });

        modelBuilder.Entity<TxnSplit>(e =>
        {
            e.ToTable("txn_split");

            e.HasOne(s => s.ParentTxn)
                .WithMany()
                .HasForeignKey(s => s.ParentTxnId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.ChildTxn)
                .WithMany()
                .HasForeignKey(s => s.ChildTxnId)
                .OnDelete(DeleteBehavior.Restrict);

            ConfigureTimestamps(e);
        });

        modelBuilder.Entity<Asset>(e =>
        {
            e.ToTable("asset", t =>
            {
                t.HasCheckConstraint(
                    "CK_asset_asset_class",
                    "asset_class IN ('gold','equity','etf','crypto','property','other')");
                t.HasCheckConstraint("CK_asset_unit", "unit IN ('gram','share','unit')");
                t.HasCheckConstraint("CK_asset_purity", "purity IN ('24k','22k','21k','18k') OR purity IS NULL");
            });

            e.HasIndex(a => a.Code).IsUnique();
            e.Property(a => a.AssetClass).HasConversion(SnakeCaseEnumConverter.For<AssetClass>());
            e.Property(a => a.Unit).HasConversion(SnakeCaseEnumConverter.For<AssetUnit>());
            e.Property(a => a.Purity).HasConversion(GoldPurityConverter.Instance);

            ConfigureTimestamps(e);
        });

        modelBuilder.Entity<Holding>(e =>
        {
            e.ToTable("holding");
            e.HasIndex(h => new { h.AccountId, h.AssetId }).IsUnique();

            e.HasOne(h => h.Account).WithMany().HasForeignKey(h => h.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(h => h.Asset).WithMany().HasForeignKey(h => h.AssetId).OnDelete(DeleteBehavior.Restrict);

            ConfigureTimestamps(e);
        });

        modelBuilder.Entity<HoldingTxn>(e =>
        {
            e.ToTable("holding_txn", t => t.HasCheckConstraint(
                "CK_holding_txn_origin",
                "origin IN ('buy','sell','gift','seed')"));

            e.Property(x => x.QtyDelta).HasConversion(DecimalTextConverter.Instance);
            e.Property(x => x.Origin).HasConversion(SnakeCaseEnumConverter.For<HoldingTxnOrigin>());

            e.HasOne(x => x.Holding).WithMany().HasForeignKey(x => x.HoldingId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.LinkedTxn).WithMany().HasForeignKey(x => x.LinkedTxnId).OnDelete(DeleteBehavior.Restrict);

            ConfigureTimestamps(e);
        });

        modelBuilder.Entity<Price>(e =>
        {
            e.ToTable("price", t => t.HasCheckConstraint(
                "CK_price_source",
                "source IN ('manual','fetched','estimated','seed')"));

            // No surrogate id — this composite key is the real identity, straight from
            // REQUIREMENTS.md §5: the same asset can have a manual price and a fetched price on
            // the same day, and both are kept.
            e.HasKey(p => new { p.AssetId, p.AsOfDate, p.Source });
            e.Property(p => p.Source).HasConversion(SnakeCaseEnumConverter.For<PriceSource>());

            e.HasOne(p => p.Asset).WithMany().HasForeignKey(p => p.AssetId).OnDelete(DeleteBehavior.Restrict);

            ConfigureTimestamps(e);
        });

        modelBuilder.Entity<Period>(e =>
        {
            e.ToTable("period", t =>
            {
                t.HasCheckConstraint("CK_period_status", "status IN ('open','closed')");
                t.HasCheckConstraint("CK_period_month", "month BETWEEN 1 AND 12");
            });

            e.HasIndex(p => new { p.Year, p.Month }).IsUnique();
            e.Property(p => p.Status).HasConversion(SnakeCaseEnumConverter.For<PeriodStatus>());

            ConfigureTimestamps(e);
        });

        modelBuilder.Entity<Snapshot>(e =>
        {
            e.ToTable("snapshot", t => t.HasCheckConstraint(
                "CK_snapshot_kind",
                "kind IN ('close','restatement')"));

            e.Property(s => s.Kind).HasConversion(SnakeCaseEnumConverter.For<SnapshotKind>());

            e.HasOne(s => s.Period).WithMany().HasForeignKey(s => s.PeriodId).OnDelete(DeleteBehavior.Restrict);

            ConfigureTimestamps(e);
        });

        modelBuilder.Entity<SnapshotLine>(e =>
        {
            e.ToTable("snapshot_line");

            e.Property(l => l.Quantity).HasConversion(DecimalTextConverter.NullableInstance);
            e.Property(l => l.FxRateUsed).HasConversion(DecimalTextConverter.NullableInstance);

            e.HasOne(l => l.Snapshot).WithMany().HasForeignKey(l => l.SnapshotId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.Account).WithMany().HasForeignKey(l => l.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.Asset).WithMany().HasForeignKey(l => l.AssetId).OnDelete(DeleteBehavior.Restrict);

            ConfigureTimestamps(e);
        });
    }

    private static void ConfigureTimestamps<T>(EntityTypeBuilder<T> e)
        where T : class, ITimestamped
    {
        e.Property(x => x.CreatedAt)
            .HasDefaultValueSql(UtcNowSql)
            .ValueGeneratedOnAdd()
            .Metadata.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);

        e.Property(x => x.UpdatedAt)
            .HasDefaultValueSql(UtcNowSql)
            .ValueGeneratedOnAddOrUpdate()
            .Metadata.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);
    }
}
