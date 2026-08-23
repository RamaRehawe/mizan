using Microsoft.EntityFrameworkCore;

namespace Mizan.Models;

public class MizanDbContext(DbContextOptions<MizanDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Txn> Txns => Set<Txn>();

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
        });

        modelBuilder.Entity<Txn>(e =>
        {
            e.ToTable("txn", t => t.HasCheckConstraint(
                "CK_txn_origin",
                "origin IN ('import','manual','split','adjustment','seed')"));

            e.Property(x => x.Origin).HasConversion(SnakeCaseEnumConverter.For<TxnOrigin>());

            // At most one live (non-superseded, non-void) txn per dedupe_key. SQLite supports a
            // real partial unique index, unlike MySQL — no generated-column workaround needed.
            e.HasIndex(x => x.DedupeKey)
                .IsUnique()
                .HasFilter("superseded_by_id IS NULL AND is_void = 0");

            e.HasOne(x => x.Account)
                .WithMany(a => a.Txns)
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne<Txn>()
                .WithMany()
                .HasForeignKey(x => x.ParentTxnId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne<Txn>()
                .WithMany()
                .HasForeignKey(x => x.SupersedesId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne<Txn>()
                .WithMany()
                .HasForeignKey(x => x.SupersededById)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
