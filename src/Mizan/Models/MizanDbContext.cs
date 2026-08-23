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

            // Not unique here — "at most one live txn per dedupe_key" now depends on TxnVoid and
            // TxnSupersession too, which SQLite can't express in a single-table partial index.
            // Enforced instead by a trigger in the InitialSchema migration.
            e.HasIndex(x => x.DedupeKey);

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
