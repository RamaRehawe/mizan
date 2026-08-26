using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Migrations
{
    /// <inheritdoc />
    public partial class NetWorth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Same reason as InitialSchema: EF Core's SQLite CreateTable() builder can't emit
            // STRICT, so all seven tables are raw SQL. Indexes below are EF-generated, unmodified.
            migrationBuilder.Sql(
                """
                CREATE TABLE "asset" (
                    "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "code" TEXT NOT NULL,
                    "name" TEXT NOT NULL,
                    "asset_class" TEXT NOT NULL,
                    "unit" TEXT NOT NULL,
                    "purity" TEXT NULL,
                    "quote_currency_code" TEXT NOT NULL,
                    "notes" TEXT NULL,
                    "created_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    "updated_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    CONSTRAINT "CK_asset_asset_class" CHECK (asset_class IN ('gold','equity','etf','crypto','property','other')),
                    CONSTRAINT "CK_asset_unit" CHECK (unit IN ('gram','share','unit')),
                    CONSTRAINT "CK_asset_purity" CHECK (purity IN ('24k','22k','21k','18k') OR purity IS NULL)
                ) STRICT
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE "period" (
                    "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "year" INTEGER NOT NULL,
                    "month" INTEGER NOT NULL,
                    "status" TEXT NOT NULL,
                    "closed_at" TEXT NULL,
                    "is_stale" INTEGER NOT NULL,
                    "note" TEXT NULL,
                    "created_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    "updated_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    CONSTRAINT "CK_period_status" CHECK (status IN ('open','closed')),
                    CONSTRAINT "CK_period_month" CHECK (month BETWEEN 1 AND 12)
                ) STRICT
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE "holding" (
                    "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "account_id" INTEGER NOT NULL,
                    "asset_id" INTEGER NOT NULL,
                    "notes" TEXT NULL,
                    "created_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    "updated_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    CONSTRAINT "fk_holding_account_account_id" FOREIGN KEY ("account_id") REFERENCES "account" ("id"),
                    CONSTRAINT "fk_holding_asset_asset_id" FOREIGN KEY ("asset_id") REFERENCES "asset" ("id")
                ) STRICT
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE "price" (
                    "asset_id" INTEGER NOT NULL,
                    "as_of_date" TEXT NOT NULL,
                    "source" TEXT NOT NULL,
                    "price_minor" INTEGER NOT NULL,
                    "currency_code" TEXT NOT NULL,
                    "created_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    "updated_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    PRIMARY KEY ("asset_id", "as_of_date", "source"),
                    CONSTRAINT "CK_price_source" CHECK (source IN ('manual','fetched','estimated','seed')),
                    CONSTRAINT "fk_price_asset_asset_id" FOREIGN KEY ("asset_id") REFERENCES "asset" ("id")
                ) STRICT
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE "snapshot" (
                    "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "period_id" INTEGER NOT NULL,
                    "taken_at" TEXT NOT NULL,
                    "kind" TEXT NOT NULL,
                    "total_net_worth_minor" INTEGER NOT NULL,
                    "payload_json" TEXT NULL,
                    "note" TEXT NULL,
                    "created_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    "updated_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    CONSTRAINT "CK_snapshot_kind" CHECK (kind IN ('close','restatement')),
                    CONSTRAINT "fk_snapshot_period_period_id" FOREIGN KEY ("period_id") REFERENCES "period" ("id")
                ) STRICT
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE "holding_txn" (
                    "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "holding_id" INTEGER NOT NULL,
                    "occurred_on" TEXT NOT NULL,
                    "qty_delta" TEXT NOT NULL,
                    "unit_cost_minor" INTEGER NULL,
                    "fee_minor" INTEGER NOT NULL,
                    "currency_code" TEXT NOT NULL,
                    "linked_txn_id" INTEGER NULL,
                    "origin" TEXT NOT NULL,
                    "note" TEXT NULL,
                    "created_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    "updated_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    CONSTRAINT "CK_holding_txn_origin" CHECK (origin IN ('buy','sell','gift','seed')),
                    CONSTRAINT "fk_holding_txn_holding_holding_id" FOREIGN KEY ("holding_id") REFERENCES "holding" ("id"),
                    CONSTRAINT "fk_holding_txn_txn_linked_txn_id" FOREIGN KEY ("linked_txn_id") REFERENCES "txn" ("id")
                ) STRICT
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE "snapshot_line" (
                    "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "snapshot_id" INTEGER NOT NULL,
                    "account_id" INTEGER NULL,
                    "asset_id" INTEGER NULL,
                    "quantity" TEXT NULL,
                    "price_minor" INTEGER NULL,
                    "price_as_of" TEXT NULL,
                    "balance_minor" INTEGER NOT NULL,
                    "balance_base_minor" INTEGER NOT NULL,
                    "fx_rate_used" TEXT NULL,
                    "created_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    "updated_at" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                    CONSTRAINT "fk_snapshot_line_snapshot_snapshot_id" FOREIGN KEY ("snapshot_id") REFERENCES "snapshot" ("id"),
                    CONSTRAINT "fk_snapshot_line_account_account_id" FOREIGN KEY ("account_id") REFERENCES "account" ("id"),
                    CONSTRAINT "fk_snapshot_line_asset_asset_id" FOREIGN KEY ("asset_id") REFERENCES "asset" ("id")
                ) STRICT
                """);

            migrationBuilder.CreateIndex(
                name: "ix_asset_code",
                table: "asset",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_holding_account_id_asset_id",
                table: "holding",
                columns: new[] { "account_id", "asset_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_holding_asset_id",
                table: "holding",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_holding_txn_holding_id",
                table: "holding_txn",
                column: "holding_id");

            migrationBuilder.CreateIndex(
                name: "ix_holding_txn_linked_txn_id",
                table: "holding_txn",
                column: "linked_txn_id");

            migrationBuilder.CreateIndex(
                name: "ix_period_year_month",
                table: "period",
                columns: new[] { "year", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_period_id",
                table: "snapshot",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_line_account_id",
                table: "snapshot_line",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_line_asset_id",
                table: "snapshot_line",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_line_snapshot_id",
                table: "snapshot_line",
                column: "snapshot_id");

            // Same pattern as InitialSchema — updated_at is bumped by a trigger, not app code.
            // price has no id column (its primary key is the composite asset_id/as_of_date/
            // source), so it needs its own WHERE clause instead of the shared "WHERE id = NEW.id"
            // the other six tables use.
            foreach (var table in new[] { "asset", "holding", "holding_txn", "period", "snapshot", "snapshot_line" })
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

            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_price_updated_at AFTER UPDATE ON "price"
                WHEN NEW.updated_at IS OLD.updated_at
                BEGIN
                    UPDATE "price" SET updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now')
                    WHERE asset_id = NEW.asset_id AND as_of_date = NEW.as_of_date AND source = NEW.source;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "holding_txn");

            migrationBuilder.DropTable(
                name: "price");

            migrationBuilder.DropTable(
                name: "snapshot_line");

            migrationBuilder.DropTable(
                name: "holding");

            migrationBuilder.DropTable(
                name: "snapshot");

            migrationBuilder.DropTable(
                name: "asset");

            migrationBuilder.DropTable(
                name: "period");
        }
    }
}
