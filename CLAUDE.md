# CLAUDE.md — Mizan

Read this before writing anything. Keep it at the repo root; it is the standing contract for
this project.

*(Name is provisional — if it changes, it changes in the repo name, the database file name,
`<title>`, and the mark in the sidebar. Nowhere else.)*

---

## What this is

A **local, single-user personal finance tracker**. One person, one machine, opened once or
twice a month. Statements are normalized to a simple CSV outside this app, uploaded here,
resolved, reconciled against the bank, and read.

It is not a product. No multi-tenancy, no signup, no auth provider, no public deployment, no
mobile app, no background workers, no queue, no cloud anything. Do not add them, do not add
abstractions "for later scale". The dataset is ~200k rows over 20 years, so correctness beats
performance in every trade-off.

Full detail lives in `docs/REQUIREMENTS.md`. That document is the source of truth for scope,
data model, and phasing. This file is the source of truth for how to write the code — including
where it overrides the requirements doc's own "recommended technology" table (§7), which was
written before the stack below was decided.

---

## Reference documents

| File | What it's for |
|---|---|
| `docs/REQUIREMENTS.md` | Numbered requirements, data model, invariants, delivery phases. Cite requirement IDs (FR-2.8, INV-3) in commits and PR descriptions. |
| `docs/prototype.html` | The UI. Open it in a browser. Port its CSS and markup — do not redesign. |

When something in the requirements is ambiguous, **ask**. Do not resolve it by guessing and
moving on.

---

## Stack

.NET (latest LTS) · ASP.NET Core MVC (Controllers + Razor views) · SQLite · Entity Framework
Core (Code First, typed) · EF Core Migrations · C# `record` types for service DTOs · HTMX ·
xUnit · `dotnet format` + Roslyn analyzers (nullable reference types on, warnings as errors for
the enabled rule set).

SQLite is a single file on disk — **no Docker, no database server, no container.** The app runs
on the host with `dotnet watch run`.

**No npm, no bundler, no `node_modules`.** HTMX and Alpine are vendored files in
`wwwroot/vendor/`, not CDN links — the app must work with the network off.

Frontend is server-rendered Razor views. HTMX handles the four places that need partial
updates: the review queue, reconcile, the import preview, and the goal simulator — served by
partial views. Alpine is for keyboard shortcuts only. If you find yourself wanting a JS
framework, you have taken a wrong turn — say so instead of building it.

---

## Hard rules

### Money
- `INTEGER` (64-bit), in **minor units** (fils, cents). Always with a `currency_code TEXT(3)`
  beside it, mapped from a C# `string`.
- Never `REAL` for money, ever. Never a C# `double` or `float` touching an amount — use `long`
  for the minor-unit integer and C#'s `decimal` only at the display boundary if unavoidable.
- Quantities and FX rates are **not** money and are **not** integers, but SQLite has no real
  `DECIMAL` type — a `NUMERIC` column silently gets IEEE-754 `REAL` storage for non-integer
  values. Store them as `TEXT` (exact decimal string) and map to C#'s `decimal` (128-bit, exact)
  via an EF Core value converter. Never let a `double` touch a quantity or a rate.
- Formatting into a display string happens in Razor views, never in services.

### Layering
- `Services/` holds **all** business logic. It must not reference `Microsoft.AspNetCore.Mvc`
  and must not know views exist. Every method takes the `DbContext` plus typed arguments and
  returns immutable `record` DTOs — not EF entities, not dictionaries, not `dynamic`.
- `Controllers/` is thin: parse, call one service, return a view or partial view. A calculation
  in a controller is a bug.
- `Models/` (or `Entities/`) is EF Core entity classes only — no business logic on them beyond
  trivial computed properties.

This separation is the whole reason the project stays changeable. Guard it.

### Data safety
- Nothing is hard-deleted. No `DELETE` outside test teardown.
- `raw_row` and `import_batch` are never updated.
- Correcting a transaction inserts a new version and sets `superseded_by_id`. Never `UPDATE`
  a txn's amount, date, or account in place.
- Every mutating service call writes an `audit_log` row.

### Dates
- `occurred_on` is a `DATE` (stored as SQLite `TEXT` in ISO-8601 `YYYY-MM-DD`) — a transaction
  date has no time zone. Map to C#'s `DateOnly`, never `DateTime`.
- `created_at` is stored as UTC ISO-8601 with fractional seconds. Map to C#'s
  `DateTimeOffset`/`DateTime` with `DateTimeKind.Utc` — never local time in the database.
- Display timezone (`Asia/Dubai`) is applied at render only.

### Migrations
- Every schema change is an EF Core migration (`dotnet ef migrations add`). No manual DDL
  outside a migration, ever.
- **Show me each migration before applying it.** EF Core's SQLite provider rewrites a table
  (copy → drop → rename) for changes SQLite's `ALTER TABLE` can't express directly, and it does
  not generate partial-unique-index filters or `STRICT` tables on its own — those need a
  reviewed `migrationBuilder.Sql(...)` call.
- Forward-only. "Rollback" means restoring a backup.

---

## SQLite specifics that will bite you

- **Partial unique index — SQLite supports this natively**, unlike MySQL. For "at most one
  active txn per `dedupe_key`":
  `CREATE UNIQUE INDEX ux_txn_dedupe_key ON txn(dedupe_key) WHERE superseded_by_id IS NULL AND
  is_void = 0;` No generated-column workaround needed. In EF Core's fluent config, use
  `.HasIndex(...).IsUnique().HasFilter("superseded_by_id IS NULL AND is_void = 0")`.
- **No array type.** Tags are `tag` + `txn_tag` join tables, never a comma-separated string.
- **Avoid a native `ENUM`** (SQLite doesn't have one anyway). Use `TEXT` + a C# `enum`
  (persisted as its string name via an EF Core converter) + a `CHECK` constraint.
- `TEXT` is fine for `raw_row.payload` JSON (use SQLite's `json_valid()` in a `CHECK`
  constraint if you want a cheap guard). Don't index into it.
- **Use `STRICT` tables** (`CREATE TABLE ... STRICT`) everywhere. SQLite is dynamically typed by
  default; without `STRICT` a money column can silently accept a float or a string. This is the
  SQLite equivalent of MySQL's `sql-mode=STRICT_ALL_TABLES` and is non-negotiable for money
  columns.
- **Turn on foreign keys per connection**: `PRAGMA foreign_keys = ON;`. SQLite ignores FK
  constraints unless this pragma is set on every connection — confirm the EF Core connection
  string or interceptor does this; don't assume the default.
- **Use WAL mode**: `PRAGMA journal_mode = WAL;`. Better concurrent read/write behavior for the
  dev server reloading against the same file.
- **Verify EF Core's `decimal` mapping.** Confirm the SQLite provider is using a string-based
  value converter for `decimal` columns, not a `double`-backed one — check the generated
  migration's column type, don't assume.
- EF Core's `DbContext` must have every entity registered (via `OnModelCreating` or
  `IEntityTypeConfiguration<T>` classes picked up by `ApplyConfigurationsFromAssembly`) or
  `dotnet ef migrations add` silently produces a migration missing that table.

---

## Testing

- Write service functions **and their tests** before any controller action exists.
- The seed generator is deterministic (a seeded `Random` from config). Tests assert **exact**
  totals against it — never "greater than zero".
- One test per invariant in `docs/REQUIREMENTS.md` §5. Those are the tests that let this
  project be refactored years from now.
- Controller tests are smoke tests: page returns 200 with seeded data.
- The test fixture setup **must refuse to run** if the target SQLite file path doesn't end in
  `_test.db`. Write that guard before any other test code.
- Never put real financial data in fixtures. Seed data is synthetic.

---

## Style

- `dotnet format` clean; Roslyn analyzers on with nullable reference types enabled
  (`<Nullable>enable</Nullable>`) and warnings treated as errors for the enabled rule set.
- `<ImplicitUsings>enable</ImplicitUsings>`; explicit types where inference hurts readability.
- Service XML-doc comments: one line on what it returns, plus any invariant it upholds.
- Comments explain **why**, never what.
- Use domain names: `Txn`, `Account`, `Holding`, `Period`, `Snapshot`. Don't flatten them into
  `Record`, `Item`, `Entry`.

---

## Working agreement

- **Build in small vertical slices**, following the delivery phases in
  `docs/REQUIREMENTS.md` §8. Phase 0 (import, transfers, reconciliation) before anything with
  a chart in it.
- Do not build ahead of what we agreed for the current slice. If you notice something a later
  phase needs, note it and move on.
- Order within a slice: migration → entity classes → services + tests → controller action →
  Razor view → extend seed data → verify by hand.
- Stop and report after each meaningful step. Don't produce a thousand lines and then ask.
- Run `dotnet format --verify-no-changes` and `dotnet test` before telling me something is done.
- If a requirement and this file conflict, this file wins for code style; the requirement wins
  for behaviour. Flag the conflict either way.
