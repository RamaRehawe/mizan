using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF Core's SQLite CreateTable() builder has no way to emit STRICT, so all six
            // tables are raw SQL. Everything below the tables (indexes, including the account
            // name and dedupe_key uniqueness) is what EF generated unmodified.
            migrationBuilder.Sql(
                """
                CREATE TABLE "account" (
                    "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "name" TEXT NOT NULL COLLATE NOCASE,
                    "type" TEXT NOT NULL,
                    "liquidity_class" TEXT NOT NULL,
                    "currency_code" TEXT NOT NULL,
                    "institution" TEXT NULL,
                    "opening_balance_minor" INTEGER NOT NULL,
                    "opening_date" TEXT NOT NULL,
                    "is_active" INTEGER NOT NULL,
                    "sort_order" INTEGER NOT NULL,
                    "created_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    "updated_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    CONSTRAINT "CK_account_type" CHECK (type IN ('cash','bank','card','broker','loan','physical_asset','receivable','other')),
                    CONSTRAINT "CK_account_liquidity_class" CHECK (liquidity_class IN ('immediate','short_term','illiquid','debt'))
                ) STRICT
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE "category" (
                    "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "parent_id" INTEGER NULL,
                    "name" TEXT NOT NULL,
                    "kind" TEXT NOT NULL,
                    "is_active" INTEGER NOT NULL,
                    "sort_order" INTEGER NOT NULL,
                    "created_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    "updated_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    CONSTRAINT "CK_category_kind" CHECK (kind IN ('income','expense','transfer','investment','adjustment')),
                    CONSTRAINT "fk_category_category_parent_id" FOREIGN KEY ("parent_id") REFERENCES "category" ("id")
                ) STRICT
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE "txn" (
                    "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "account_id" INTEGER NOT NULL,
                    "occurred_on" TEXT NOT NULL,
                    "booked_on" TEXT NULL,
                    "amount_minor" INTEGER NOT NULL,
                    "currency_code" TEXT NOT NULL,
                    "description_raw" TEXT NOT NULL,
                    "description_norm" TEXT NULL,
                    "category_id" INTEGER NULL,
                    "origin" TEXT NOT NULL,
                    "source_detail" TEXT NULL,
                    "dedupe_key" TEXT NOT NULL,
                    "created_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    "updated_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    CONSTRAINT "CK_txn_origin" CHECK (origin IN ('import','manual','split','adjustment','seed')),
                    CONSTRAINT "fk_txn_account_account_id" FOREIGN KEY ("account_id") REFERENCES "account" ("id"),
                    CONSTRAINT "fk_txn_category_category_id" FOREIGN KEY ("category_id") REFERENCES "category" ("id")
                ) STRICT
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE "txn_split" (
                    "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "parent_txn_id" INTEGER NOT NULL,
                    "child_txn_id" INTEGER NOT NULL,
                    "created_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    "updated_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    CONSTRAINT "fk_txn_split_txn_parent_txn_id" FOREIGN KEY ("parent_txn_id") REFERENCES "txn" ("id"),
                    CONSTRAINT "fk_txn_split_txn_child_txn_id" FOREIGN KEY ("child_txn_id") REFERENCES "txn" ("id")
                ) STRICT
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE "txn_supersession" (
                    "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "old_txn_id" INTEGER NOT NULL,
                    "new_txn_id" INTEGER NOT NULL,
                    "created_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    "updated_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    CONSTRAINT "fk_txn_supersession_txn_old_txn_id" FOREIGN KEY ("old_txn_id") REFERENCES "txn" ("id"),
                    CONSTRAINT "fk_txn_supersession_txn_new_txn_id" FOREIGN KEY ("new_txn_id") REFERENCES "txn" ("id")
                ) STRICT
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE "txn_void" (
                    "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "txn_id" INTEGER NOT NULL,
                    "reason" TEXT NOT NULL,
                    "created_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    "updated_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    CONSTRAINT "fk_txn_void_txn_txn_id" FOREIGN KEY ("txn_id") REFERENCES "txn" ("id")
                ) STRICT
                """);

            migrationBuilder.CreateIndex(
                name: "ix_account_name",
                table: "account",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_category_parent_id",
                table: "category",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_txn_account_id",
                table: "txn",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_txn_category_id",
                table: "txn",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_txn_dedupe_key",
                table: "txn",
                column: "dedupe_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_txn_split_child_txn_id",
                table: "txn_split",
                column: "child_txn_id");

            migrationBuilder.CreateIndex(
                name: "ix_txn_split_parent_txn_id",
                table: "txn_split",
                column: "parent_txn_id");

            migrationBuilder.CreateIndex(
                name: "ix_txn_supersession_new_txn_id",
                table: "txn_supersession",
                column: "new_txn_id");

            migrationBuilder.CreateIndex(
                name: "ix_txn_supersession_old_txn_id",
                table: "txn_supersession",
                column: "old_txn_id");

            migrationBuilder.CreateIndex(
                name: "ix_txn_void_txn_id",
                table: "txn_void",
                column: "txn_id");

            // SQLite has no ON UPDATE CURRENT_TIMESTAMP column clause, so updated_at is bumped
            // by a trigger per table instead. The WHEN guard is belt-and-suspenders: SQLite
            // doesn't re-fire a trigger's own UPDATE unless PRAGMA recursive_triggers is on
            // (off by default), but this also means a caller who explicitly sets updated_at
            // isn't silently overridden.
            foreach (var table in new[] { "account", "category", "txn", "txn_void", "txn_supersession", "txn_split" })
            {
                migrationBuilder.Sql(
                    $"""
                    CREATE TRIGGER trg_{table}_updated_at AFTER UPDATE ON "{table}"
                    WHEN NEW.updated_at IS OLD.updated_at
                    BEGIN
                        UPDATE "{table}" SET updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now') WHERE id = NEW.id;
                    END
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "txn_split");

            migrationBuilder.DropTable(
                name: "txn_supersession");

            migrationBuilder.DropTable(
                name: "txn_void");

            migrationBuilder.DropTable(
                name: "txn");

            migrationBuilder.DropTable(
                name: "account");

            migrationBuilder.DropTable(
                name: "category");
        }
    }
}
