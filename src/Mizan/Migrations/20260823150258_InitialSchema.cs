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
            // EF Core's SQLite CreateTable() builder has no way to emit STRICT, so these three
            // tables are raw SQL instead — everything else below (indexes, including the partial
            // unique one) is what EF generated unmodified, since HasFilter() maps to SQLite's
            // native partial-index WHERE clause without any help needed.
            migrationBuilder.Sql(
                """
                CREATE TABLE "account" (
                    "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "name" TEXT NOT NULL,
                    "type" TEXT NOT NULL,
                    "liquidity_class" TEXT NOT NULL,
                    "currency_code" TEXT NOT NULL,
                    "institution" TEXT NULL,
                    "opening_balance_minor" INTEGER NOT NULL,
                    "opening_date" TEXT NOT NULL,
                    "is_active" INTEGER NOT NULL,
                    "sort_order" INTEGER NOT NULL,
                    "created_at" TEXT NOT NULL,
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
                    "created_at" TEXT NOT NULL,
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
                    "is_void" INTEGER NOT NULL,
                    "void_reason" TEXT NULL,
                    "parent_txn_id" INTEGER NULL,
                    "version" INTEGER NOT NULL,
                    "supersedes_id" INTEGER NULL,
                    "superseded_by_id" INTEGER NULL,
                    "dedupe_key" TEXT NOT NULL,
                    "created_at" TEXT NOT NULL,
                    CONSTRAINT "CK_txn_origin" CHECK (origin IN ('import','manual','split','adjustment','seed')),
                    CONSTRAINT "fk_txn_account_account_id" FOREIGN KEY ("account_id") REFERENCES "account" ("id"),
                    CONSTRAINT "fk_txn_category_category_id" FOREIGN KEY ("category_id") REFERENCES "category" ("id"),
                    CONSTRAINT "fk_txn_txn_parent_txn_id" FOREIGN KEY ("parent_txn_id") REFERENCES "txn" ("id"),
                    CONSTRAINT "fk_txn_txn_superseded_by_id" FOREIGN KEY ("superseded_by_id") REFERENCES "txn" ("id"),
                    CONSTRAINT "fk_txn_txn_supersedes_id" FOREIGN KEY ("supersedes_id") REFERENCES "txn" ("id")
                ) STRICT
                """);

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
                unique: true,
                filter: "superseded_by_id IS NULL AND is_void = 0");

            migrationBuilder.CreateIndex(
                name: "ix_txn_parent_txn_id",
                table: "txn",
                column: "parent_txn_id");

            migrationBuilder.CreateIndex(
                name: "ix_txn_superseded_by_id",
                table: "txn",
                column: "superseded_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_txn_supersedes_id",
                table: "txn",
                column: "supersedes_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "txn");

            migrationBuilder.DropTable(
                name: "account");

            migrationBuilder.DropTable(
                name: "category");
        }
    }
}
