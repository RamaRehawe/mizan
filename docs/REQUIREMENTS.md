# Personal Finance Tracker — Requirements Specification

**Version:** 0.1 (draft for build)
**Audience:** single user (owner + only operator + only developer)
**Deployment:** local-only, single machine
**Usage cadence:** weekly or monthly sessions, not daily

---

## 1. Purpose and goals

A private, local, single-user system that ingests raw financial exports (Excel/CSV), turns them into a trustworthy ledger, and answers planning questions.

### 1.1 Primary goals

| # | Goal | Success looks like |
|---|---|---|
| G1 | Know where the money went | Every month categorized, with a number I trust to ±1% |
| G2 | Know what I'm worth | Net worth across cash, bank, gold, investments, minus debt, tracked over time |
| G3 | Know how much I actually save | Savings rate, monthly and 12-month rolling |
| G4 | Know if I can afford a planned purchase | Feasibility answer with a date and an impact on emergency runway |
| G5 | Never lose data | Original files reproducible; database rebuildable from raw inputs |
| G6 | Low operating cost | A monthly session takes under 30 minutes |

### 1.2 Design principles

1. **Append-only truth.** Raw input is immutable. Corrections are new records, never overwrites.
2. **Derivation over storage.** If a number can be computed (gold value, net worth, category totals), compute it. Store only inputs and dated snapshots.
3. **Re-runnable.** Enrichment can be recomputed over the entire history at any time without touching source data.
4. **Explainable.** For any number in any report, I can drill to the source rows and the rule that produced the classification.
5. **Batch-first UX.** The app is designed around a periodic session, not around logging a coffee.
6. **Boring technology.** Single process, single database, no distributed anything.

### 1.3 Non-goals (explicitly out of scope for v1–v4)

- Multi-user, sharing, permissions, roles
- Bank API / Open Banking / screen-scraping integrations
- Mobile app or responsive-first design (desktop browser only)
- Real-time or intraday price updates
- Tax filing, invoicing, or accounting compliance output
- Automated trading, brokerage integration, or order placement
- Public hosting or internet exposure

---

## 2. Operating model

The application is used in **sessions**. A session is the unit of work; the UI should be built around it.

### 2.1 The monthly close ritual (primary workflow)

```
1. Export statements from banks / cards / broker         (outside the app)
2. Upload files                                          → Import
3. Resolve the review queue                              → Classify / link transfers
4. Enter stated balances from statements                 → Reconcile
5. Enter or refresh prices (gold, holdings) and FX       → Value
6. Freeze the month                                      → Snapshot
7. Read the monthly report, update goals                 → Plan
```

**REQ-OP-1 (MUST):** The home screen shows the current state of the ritual — which steps are done, which are pending, for the current open period.
**REQ-OP-2 (MUST):** Any step can be re-run. Re-running import with the same file is a no-op.
**REQ-OP-3 (SHOULD):** A weekly "light session" path exists: import + review queue only, no close.
**REQ-OP-4 (SHOULD):** The app tells me what's stale: "Gold price last updated 47 days ago", "March not closed", "Bank X has no import since Feb".

### 2.2 Periods

- A **period** is a calendar month, in a configured timezone (default `Asia/Dubai`).
- A period is `open` → `closed`. Closing freezes snapshots; it does not lock data entry.
- Late data arriving for a closed period is allowed and **MUST** mark that period `stale`, prompting a re-close.

---

## 3. Glossary

| Term | Meaning |
|---|---|
| **Account** | A container that holds value: a bank account, a cash stash, a credit card, a broker account, a loan, a physical asset store |
| **Bucket** | A virtual subdivision of an account (e.g. "emergency" inside the main bank account) |
| **Transaction (txn)** | A dated movement of money affecting one account |
| **Transfer** | A pair of transactions moving value between two of my own accounts; net effect on net worth is zero |
| **Enrichment** | Category, merchant, tags, and notes attached to a transaction |
| **Holding** | A quantity of a non-cash asset (gold grams, share units) inside an account |
| **Price** | A dated unit value for an asset |
| **Snapshot** | A frozen, dated record of balances and valuations |
| **Goal** | A planned future purchase or savings target |

---

## 4. Functional requirements

### FR-1 — Accounts and buckets

- **FR-1.1 (MUST)** CRUD for accounts with: name, type, currency, institution, opening balance, opening date, active flag, display order.
- **FR-1.2 (MUST)** Account types: `cash`, `bank`, `card`, `broker`, `loan`, `physical_asset`, `receivable`, `other`.
- **FR-1.3 (MUST)** Accounts are never deleted, only deactivated. Deactivated accounts are hidden from entry screens but retained in history.
- **FR-1.4 (MUST)** Each account has a `liquidity_class`: `immediate` (cash on hand, current account), `short_term` (savings, easily sold), `illiquid` (gold, long-term investments, property), `debt`.
- **FR-1.5 (SHOULD)** Buckets: an account may have named virtual buckets with target amounts. Bucket balances must sum to the account balance; the remainder sits in an implicit `unallocated` bucket.
- **FR-1.6 (SHOULD)** A bucket may be flagged `is_emergency_fund` — used by the runway calculation.
- **FR-1.7 (COULD)** Bucket allocation is set at close time by simple assignment, not by tagging every transaction.

### FR-2 — Import

- **FR-2.1 (MUST)** Upload one or more `.xlsx` / `.csv` files through the UI.
- **FR-2.2 (MUST)** Every uploaded file is stored byte-for-byte on disk under a content-addressed path, with its SHA-256 recorded.
- **FR-2.3 (MUST)** Re-uploading a byte-identical file is detected and rejected with a clear message, not silently duplicated.
- **FR-2.4 (MUST)** Each file is parsed into `raw_row` records: one row per source row, full original content preserved as JSON, plus sheet name and row number. Raw rows are immutable.
- **FR-2.5 (MUST)** An import is bound to a **source profile**, which defines: which sheet, which header row, column mapping, date format, decimal/thousand separators, sign convention (single signed amount column vs. separate debit/credit columns), and default account.
- **FR-2.6 (MUST)** Source profiles are stored and reusable. Adding a new bank means adding a profile, not changing code.
- **FR-2.7 (MUST)** Import runs in two phases: **stage** (parse and show a preview with warnings, nothing committed) and **commit**. I must be able to abort after seeing the preview.
- **FR-2.8 (MUST)** Deduplication: each canonical transaction gets a `dedupe_key = hash(account_id, occurred_on, amount_minor, normalized_description, occurrence_index)`. `occurrence_index` distinguishes genuinely repeated identical rows on the same day. Rows whose key already exists are skipped and reported as "already present".
- **FR-2.9 (MUST)** Overlapping date ranges across files are expected and must be handled by FR-2.8, not by asking me to trim files.
- **FR-2.10 (MUST)** The import result screen reports: rows read, transactions created, duplicates skipped, rows failed with reasons.
- **FR-2.11 (SHOULD)** Failed rows are retained in a quarantine list, fixable and re-committable without re-uploading the file.
- **FR-2.12 (SHOULD)** Manual single-transaction entry (for cash spending that appears in no statement).
- **FR-2.13 (SHOULD)** Bulk paste entry: paste rows into a textarea, map columns, commit — for quick ad-hoc lists.
- **FR-2.14 (COULD)** Profile auto-detection by matching the header signature of an uploaded file.

#### FR-2.15 — Canonical import template (MUST)

A built-in profile accepting a hand-maintained sheet with these columns:

| Column | Required | Notes |
|---|---|---|
| `date` | yes | ISO `YYYY-MM-DD` preferred |
| `account` | yes | Matches account name or alias |
| `amount` | yes | Signed: negative = money out |
| `currency` | no | Defaults to account currency |
| `description` | yes | Free text |
| `counter_account` | no | Present ⇒ this row is a transfer |
| `category` | no | Pre-classification hint |
| `tags` | no | Comma-separated |
| `note` | no | Free text |
| `external_id` | no | Bank reference, strengthens dedupe |

### FR-3 — Canonical transactions

- **FR-3.1 (MUST)** Fields: `occurred_on` (value date), `booked_on` (posting date, nullable), account, `amount_minor` (BIGINT, signed, negative = outflow), currency, raw description, normalized description, `external_id`, `dedupe_key`, source raw row reference.
- **FR-3.2 (MUST)** Transactions are versioned. Editing creates a new version row; the old version is marked superseded and remains queryable. No physical deletes.
- **FR-3.3 (MUST)** A transaction may be marked `void` (e.g. a bank error reversed) — excluded from reports but retained.
- **FR-3.4 (MUST)** Splitting: one transaction may be split into N child transactions whose amounts sum exactly to the parent. The parent is retained and excluded from reporting; children carry the enrichment.
- **FR-3.5 (SHOULD)** Merging: two transactions may be linked as a duplicate pair, one designated primary.
- **FR-3.6 (MUST)** Every transaction records `created_at` and the origin (`import`, `manual`, `split`, `adjustment`).

### FR-4 — Transfers

- **FR-4.1 (MUST)** A transfer links two transactions in different accounts. Both are flagged as transfer legs and excluded from income and expense totals.
- **FR-4.2 (MUST)** A transfer may be created from a single transaction where the counterparty account has no import (e.g. cash withdrawal to a wallet with no statement) — the system generates the balancing leg automatically.
- **FR-4.3 (MUST)** Credit card handling: card purchases are expenses on the card account at purchase date; the monthly card payment from the bank is a **transfer** to the card account. Reports must never count both.
- **FR-4.4 (MUST)** Auto-suggest transfer candidates: opposite-signed transactions of matching amount within a configurable window (default ±5 days), optionally across currencies using FX with a tolerance.
- **FR-4.5 (MUST)** A one-click "link as transfer" action from the review queue.
- **FR-4.6 (SHOULD)** An unmatched-transfer warning at close time: "3 transactions look like transfer legs but are unlinked."
- **FR-4.7 (MUST)** Currency-crossing transfers (AED → USD broker funding) record both leg amounts as-is plus the implied rate; the difference is not treated as income or expense but as an FX effect.

### FR-5 — Enrichment and rules

- **FR-5.1 (MUST)** Enrichment records: category, merchant (normalized payee), tags (many), note, counterparty/person, and are stored append-only with `source` = `rule` | `manual` | `import_hint`.
- **FR-5.2 (MUST)** The effective enrichment for a transaction is the most recent record; manual always outranks rule regardless of time.
- **FR-5.3 (MUST)** Rules: ordered by priority, each with a matcher (description regex/contains, amount range, account, direction, date range) and a set of values to apply.
- **FR-5.4 (MUST)** Rules are re-runnable across all history, on demand, with a preview of how many transactions would change before applying.
- **FR-5.5 (MUST)** Manual classification offers "create a rule from this" pre-filled from the transaction, and shows how many existing transactions the proposed rule would also match.
- **FR-5.6 (MUST)** A manual override is never silently reverted by a later rule run.
- **FR-5.7 (MUST)** Category tree: two levels minimum (group → category), each with `kind` = `income` | `expense` | `transfer` | `investment` | `adjustment`.
- **FR-5.8 (MUST)** Categories carry an `essential` flag (rent, groceries, utilities, insurance, debt service) — used for runway and forecast.
- **FR-5.9 (SHOULD)** Categories carry a `recurrence_expectation`: `fixed_monthly`, `variable_monthly`, `annual`, `irregular`.
- **FR-5.10 (SHOULD)** Merchant normalization table mapping raw description fragments to a clean merchant name.
- **FR-5.11 (SHOULD)** Free-form tags orthogonal to categories: `trip:georgia`, `project:home`, `person:mom`, `reimbursable`.
- **FR-5.12 (MUST)** Every transaction has an effective category; unclassified ones fall into a reserved `UNCATEGORIZED` category that reports surface loudly.

### FR-6 — Review queue

- **FR-6.1 (MUST)** A single work-list of everything needing attention, with counts by reason:
  - uncategorized transactions
  - probable transfers not linked
  - possible duplicates
  - amounts above a configurable threshold (verify large items)
  - rows quarantined during import
  - transactions whose rule match was low-confidence
- **FR-6.2 (MUST)** Keyboard-driven: classify, link, split, skip, without leaving the keyboard.
- **FR-6.3 (MUST)** Bulk actions on a filtered selection.
- **FR-6.4 (SHOULD)** Queue is ordered by impact (largest amounts first) so a partial session still resolves most of the money.
- **FR-6.5 (SHOULD)** "Snooze" an item so it stops blocking the close.

### FR-7 — Reconciliation

- **FR-7.1 (MUST)** For any account and date, record a **stated balance** taken from the real statement.
- **FR-7.2 (MUST)** The system computes the balance from opening balance + transactions and displays the delta.
- **FR-7.3 (MUST)** When the delta is non-zero, the app assists: list transactions near the date, highlight amounts equal to the delta, offer to create an explicit `adjustment` transaction.
- **FR-7.4 (MUST)** Adjustments are a distinct category kind, always visible in reports as "unexplained", never silently folded into expenses.
- **FR-7.5 (MUST)** An account is `reconciled through <date>` once a stated balance matches. This status is shown per account on the dashboard.
- **FR-7.6 (SHOULD)** Warn if closing a period with any account unreconciled for that period.
- **FR-7.7 (SHOULD)** Cash-on-hand reconciliation: enter counted cash; the difference becomes an adjustment categorized as `cash drift`.

### FR-8 — Holdings, prices, gold, FX

- **FR-8.1 (MUST)** Holdings model quantity, not value: `asset`, `quantity`, `unit`, held inside an account.
- **FR-8.2 (MUST)** Asset registry: symbol/code, name, class (`gold`, `equity`, `etf`, `crypto`, `property`, `other`), unit (`gram`, `share`, `unit`), quote currency, and for gold a `purity` (24k / 22k / 21k / 18k).
- **FR-8.3 (MUST)** Holding transactions: dated `qty_delta` with `unit_cost_minor` and fees — supports buys, sells, and gifts. Current quantity is the sum of deltas, never stored directly.
- **FR-8.4 (MUST)** Prices: dated unit price per asset with a `source` label (`manual`, `fetched`, `estimated`). Manual entry is always available and always sufficient — no integration is required for the system to function.
- **FR-8.5 (MUST)** Valuation of a holding on date D uses the latest price at or before D, and reports the price date so stale valuations are visible.
- **FR-8.6 (MUST)** Gold: purity-adjusted valuation, i.e. `grams × purity_factor × price_per_gram_24k`, unless a purity-specific price is supplied.
- **FR-8.7 (MUST)** Cost basis and unrealized gain per holding (weighted average cost method; document the choice).
- **FR-8.8 (MUST)** FX rates: dated `base → quote` rates, manual entry supported. Every non-base-currency amount is reportable in the base currency using the rate at transaction date; balances are revalued at the reporting date rate.
- **FR-8.9 (MUST)** Base/reporting currency is a single configured setting.
- **FR-8.10 (SHOULD)** A "prices needing refresh" list showing assets whose latest price is older than N days.
- **FR-8.11 (COULD)** Optional fetcher for gold and equity prices, run manually from the UI, writing rows with `source='fetched'`. Must degrade gracefully to manual when offline.
- **FR-8.12 (SHOULD)** Broker statement import populates holding transactions, linking this system to existing trading records.

### FR-9 — Net worth

- **FR-9.1 (MUST)** Net worth = Σ(cash-type account balances) + Σ(holding valuations) − Σ(debt balances), all in base currency.
- **FR-9.2 (MUST)** Monthly snapshots freeze: per-account balance, per-holding quantity and price used, FX rates used, and the resulting totals. Historic net worth is read from snapshots, never recomputed with today's prices.
- **FR-9.3 (MUST)** Net worth chart over time with a stacked breakdown by asset class and by liquidity class.
- **FR-9.4 (SHOULD)** Month-over-month change decomposed into: net savings (inflow − outflow), market movement (price change), FX movement, and unexplained (adjustments). This decomposition is one of the most valuable outputs of the whole system.
- **FR-9.5 (SHOULD)** Snapshots can be regenerated for a past month if data changed, with an audit note recording that a restatement occurred.

### FR-10 — Reports and analysis

- **FR-10.1 (MUST)** Monthly summary: income, expense, net, savings rate, by category with drill-down to transactions.
- **FR-10.2 (MUST)** Category trend over 12+ months (table and chart), with a column for the 3-month and 12-month average.
- **FR-10.3 (MUST)** Every number in every report drills through to the underlying transaction list. No dead-end aggregates.
- **FR-10.4 (MUST)** Savings rate: `(income − expense) / income`, monthly and 12-month rolling, with transfers and investment purchases correctly excluded from expense.
- **FR-10.5 (MUST)** Income analysis: by source, with a concentration measure (share from largest source).
- **FR-10.6 (MUST)** Emergency runway: `liquid_assets ÷ trailing_6_month_average_essential_expense`, expressed in months, with the inputs shown.
- **FR-10.7 (MUST)** Asset allocation: percentage by asset class and by liquidity class, current and over time.
- **FR-10.8 (SHOULD)** Recurring transaction detection: cluster by merchant + approximate amount + cadence; list detected subscriptions with monthly and annualized cost, last seen date, and a flag for ones that stopped or changed price.
- **FR-10.9 (SHOULD)** Annualized view: total spend per category for a rolling 12 months, which is the correct lens for lumpy annual items.
- **FR-10.10 (SHOULD)** Anomaly list per month: categories more than X% above their trailing average, new merchants, largest transactions.
- **FR-10.11 (SHOULD)** Tag-based reporting: total spend for `trip:georgia` across all categories and accounts.
- **FR-10.12 (COULD)** Net worth milestones and time-to-target projection.
- **FR-10.13 (COULD)** Zakat basis report: qualifying cash + gold + investment value on a chosen date, with the components listed so the calculation is transparent and auditable.

### FR-11 — Goals and purchase TODOs

- **FR-11.1 (MUST)** A goal has: name, target amount, currency, target date (optional), priority, status (`idea`, `planned`, `saving`, `purchased`, `abandoned`), category, and notes.
- **FR-11.2 (MUST)** Goals are ranked, and the ranking is explicit — no goal exists without a position relative to the others.
- **FR-11.3 (MUST)** Feasibility answer per goal: given the trailing average monthly surplus, the projected date the goal is affordable, and the shortfall at the target date.
- **FR-11.4 (MUST)** Impact simulation: "if I buy this today, my emergency runway drops from X to Y months and my liquid cash becomes Z." This is the core value of the feature.
- **FR-11.5 (MUST)** A goal may be linked to a funding bucket, showing progress as funded vs. remaining.
- **FR-11.6 (SHOULD)** "Top up emergency fund to N months" is expressible as a goal, competing with purchase goals in the same ranking.
- **FR-11.7 (SHOULD)** On purchase, the goal is linked to the actual transaction(s), recording planned vs. actual amount and planned vs. actual date.
- **FR-11.8 (SHOULD)** Goal history is retained, including abandoned ones, with the reason.
- **FR-11.9 (COULD)** A recurring "wish list review" prompt at close time: still want this?

### FR-12 — Commitments, budgets, obligations

- **FR-12.1 (MUST)** Register of known future obligations: rent, insurance renewals, visa/licence renewals, school fees, annual subscriptions, loan instalments, BNPL/card instalment plans — each with amount, currency, due date or cadence, and account.
- **FR-12.2 (MUST)** A 12-month calendar of committed outflows derived from that register.
- **FR-12.3 (MUST)** Debt tracking: outstanding balance, instalment schedule, remaining instalments, total remaining commitment.
- **FR-12.4 (SHOULD)** Budgets per category per period, with actual vs. budget and variance. Budgets are optional and must not block anything else.
- **FR-12.5 (SHOULD)** Amortized view: annual items spread across 12 months so a single month isn't distorted. Presented alongside, never instead of, the cash view.
- **FR-12.6 (SHOULD)** Obligations can be matched to actual transactions when they occur, so "did I pay this?" is answerable.

### FR-13 — Forecast

- **FR-13.1 (SHOULD)** 12-month projection of liquid balances from: trailing average income, trailing average non-essential expense, essential expense, registered obligations (FR-12.1), and planned goal purchases.
- **FR-13.2 (SHOULD)** Show the projection as a line with the lowest projected point highlighted (the real risk moment).
- **FR-13.3 (SHOULD)** Assumptions panel where each input can be overridden manually (e.g. "assume income +10% from June").
- **FR-13.4 (COULD)** Scenario save/compare: "with car purchase" vs. "without".
- **FR-13.5 (MUST, if built)** The forecast must state its assumptions on the same screen as the numbers. Never present a projection as a fact.

### FR-14 — Attachments, notes, audit

- **FR-14.1 (SHOULD)** Attach files (receipt, statement PDF, contract) to a transaction, account, holding, obligation, or goal. Stored on disk, referenced by hash.
- **FR-14.2 (MUST)** Free-text notes on transactions, accounts, goals, and periods. A period-level note ("March: bonus received, moved house") is high value for future me.
- **FR-14.3 (MUST)** Audit log of every mutating action: what, when, before, after. Since there is one user, this is for forensics and trust, not accountability.

### FR-15 — Browsing and search

- **FR-15.1 (MUST)** Transaction browser with filters: date range, account, category, tag, merchant, amount range, direction, transfer/non-transfer, enrichment source, text search.
- **FR-15.2 (MUST)** Saved filters/views.
- **FR-15.3 (MUST)** Every list is exportable to CSV.
- **FR-15.4 (SHOULD)** A raw-data view: see the original imported row behind any transaction, and the file it came from.
- **FR-15.5 (COULD)** SQL console for ad-hoc read-only queries — genuinely useful given the single technical user.

### FR-16 — Data lifecycle

- **FR-16.1 (MUST)** Full export: all tables to a portable format (CSV bundle or SQL dump) on demand.
- **FR-16.2 (MUST)** Automated local backup on a schedule and on every close, retained with rotation.
- **FR-16.3 (MUST)** Documented and tested restore procedure. A backup that has never been restored does not count.
- **FR-16.4 (MUST)** Rebuild-from-raw capability: given the archived original files plus the profiles, rules, and manual overrides (all of which are data, not code), the canonical layer can be regenerated from scratch.
- **FR-16.5 (SHOULD)** Manual overrides and rules are themselves exportable, since they represent years of accumulated judgment and are the hardest data to recreate.

---

## 5. Data model

Money is always `BIGINT` in minor units with an explicit currency column. Never floating point. Never a bare number without its currency.

```sql
-- Reference
currency(code PK, minor_unit_digits, symbol)
fx_rate(as_of_date, base_code, quote_code, rate NUMERIC(20,10), source, PK(as_of_date,base_code,quote_code))
setting(key PK, value_json)

-- Structure
account(id PK, name, alias[], type, liquidity_class, currency_code,
        institution, opening_balance_minor, opening_date,
        is_active, sort_order, notes, created_at)
bucket(id PK, account_id FK, name, target_minor, is_emergency_fund, sort_order)
category(id PK, parent_id FK NULL, name, kind, is_essential,
         recurrence_expectation, is_active, sort_order)
merchant(id PK, canonical_name, notes)
merchant_pattern(id PK, merchant_id FK, pattern, is_regex)

-- Ingestion (immutable)
import_batch(id PK, filename, sha256, stored_path, profile_id FK,
             uploaded_at, status, rows_read, rows_committed,
             rows_skipped, rows_failed, notes)
source_profile(id PK, name, account_id FK NULL, config_json, is_active)
raw_row(id PK, batch_id FK, sheet_name, row_number, payload_json, row_hash)
        -- UNIQUE(batch_id, sheet_name, row_number); no UPDATE, no DELETE

-- Canonical ledger (versioned, append-only)
txn(id PK, raw_row_id FK NULL, account_id FK,
    occurred_on, booked_on NULL,
    amount_minor BIGINT, currency_code,
    amount_base_minor BIGINT, fx_rate_used NUMERIC(20,10),
    description_raw, description_norm, external_id NULL,
    dedupe_key, origin, is_void, void_reason,
    parent_txn_id FK NULL,          -- set on split children
    version INT, supersedes_id FK NULL, superseded_by_id FK NULL,
    created_at)
    -- UNIQUE(dedupe_key) WHERE superseded_by_id IS NULL AND NOT is_void
transfer(id PK, from_txn_id FK, to_txn_id FK, implied_rate NULL,
         matched_by, confidence, created_at)
txn_enrichment(id PK, txn_id FK, category_id FK NULL, merchant_id FK NULL,
               tags TEXT[], note, counterparty,
               source, rule_id FK NULL, created_at)
               -- append-only; effective = latest, manual outranks rule
rule(id PK, name, priority INT, matcher_json, sets_json,
     is_enabled, created_at, last_run_at, match_count)

-- Assets
asset(id PK, code, name, asset_class, unit, purity NULL,
      quote_currency_code, notes)
holding(id PK, account_id FK, asset_id FK, notes)
holding_txn(id PK, holding_id FK, occurred_on, qty_delta NUMERIC(24,8),
            unit_cost_minor, fee_minor, currency_code,
            linked_txn_id FK NULL, origin, note)
price(asset_id FK, as_of_date, price_minor, currency_code, source,
      PK(asset_id, as_of_date, source))

-- Verification
balance_statement(id PK, account_id FK, as_of_date,
                  stated_balance_minor, source_note, created_at)
reconciliation(id PK, account_id FK, as_of_date, computed_minor,
               stated_minor, delta_minor, status, resolved_by_txn_id NULL,
               created_at)

-- Periods and snapshots
period(id PK, year, month, status, closed_at, is_stale, note)
snapshot(id PK, period_id FK, taken_at, kind, payload_json, total_net_worth_minor)
snapshot_line(id PK, snapshot_id FK, account_id NULL, asset_id NULL,
              quantity NULL, price_minor NULL, price_as_of NULL,
              balance_minor, balance_base_minor, fx_rate_used)

-- Planning
goal(id PK, name, target_minor, currency_code, target_date NULL,
     priority INT, status, category_id NULL, funding_bucket_id NULL,
     note, created_at, closed_at NULL, actual_minor NULL,
     actual_date NULL, outcome_note)
obligation(id PK, name, amount_minor, currency_code, cadence,
           next_due_date, account_id FK, category_id FK,
           end_date NULL, is_active, note)
obligation_payment(id PK, obligation_id FK, due_date, txn_id FK NULL, status)
budget(id PK, category_id FK, period_year, period_month, amount_minor)

-- Cross-cutting
attachment(id PK, sha256, stored_path, original_filename, mime, size_bytes,
           entity_type, entity_id, created_at)
audit_log(id PK, at, entity_type, entity_id, action, before_json, after_json)
```

### Key invariants

| # | Invariant |
|---|---|
| INV-1 | `raw_row` and `import_batch` rows are never updated or deleted |
| INV-2 | A `txn` is never updated; corrections insert a new version and set `superseded_by_id` |
| INV-3 | Exactly one non-superseded, non-void `txn` per `dedupe_key` |
| INV-4 | Split children's `amount_minor` sums exactly to the parent's |
| INV-5 | Both legs of a `transfer` are excluded from income and expense aggregates |
| INV-6 | Holding quantity is always `SUM(qty_delta)` — never a stored column |
| INV-7 | Historic valuations use the price effective on the snapshot date, not the latest price |
| INV-8 | Every reported figure carries a currency; no mixed-currency sums without a stated rate |
| INV-9 | Bucket balances sum to the parent account balance |
| INV-10 | Reconciliation deltas are either explained by a transaction or recorded as a visible adjustment — never absorbed silently |

---

## 6. Non-functional requirements

### NFR-1 — Deployment and environment
- **NFR-1.1 (MUST)** Runs entirely on one machine. `docker compose up` (or a single command equivalent) brings up the full stack.
- **NFR-1.2 (MUST)** Binds to `127.0.0.1` only by default. Not reachable from the local network without an explicit config change.
- **NFR-1.3 (MUST)** No outbound network calls required for core functionality. Optional price fetching is the only exception and must be disableable.
- **NFR-1.4 (SHOULD)** Starts cold in under 30 seconds; the app can be off between sessions.
- **NFR-1.5 (SHOULD)** Data directory (database, uploaded files, attachments, backups) is a single configurable path, so it can be placed on an encrypted or synced volume.

### NFR-2 — Security and privacy
- **NFR-2.1 (MUST)** No third-party analytics, telemetry, or error reporting.
- **NFR-2.2 (MUST)** Full-disk or volume encryption assumed and documented as a prerequisite.
- **NFR-2.3 (SHOULD)** A simple local passcode gate on the UI to protect against casual access to an unlocked machine.
- **NFR-2.4 (MUST)** Backups are encrypted if they leave the machine (e.g. to cloud sync).
- **NFR-2.5 (MUST)** Secrets and config are not committed to version control.

### NFR-3 — Reliability and data safety
- **NFR-3.1 (MUST)** Every import commits in a single transaction — all rows or none.
- **NFR-3.2 (MUST)** A backup is taken automatically before every import commit and every bulk rule re-run.
- **NFR-3.3 (MUST)** Bulk operations show a preview and a count before executing.
- **NFR-3.4 (MUST)** Database migrations are versioned and forward-only, with a tested rollback path via backup restore.

### NFR-4 — Performance
- **NFR-4.1 (MUST)** Designed for up to ~200k transactions and ~20 years of history — trivially small; correctness beats optimization everywhere.
- **NFR-4.2 (SHOULD)** Any report renders in under 2 seconds.
- **NFR-4.3 (SHOULD)** Import of a 5,000-row file completes in under 30 seconds.

### NFR-5 — Usability
- **NFR-5.1 (MUST)** Desktop browser, keyboard-friendly, dense tables over cards.
- **NFR-5.2 (MUST)** Amounts always right-aligned, monospaced, with currency shown; negative amounts visually distinct.
- **NFR-5.3 (MUST)** Dates displayed in one consistent format throughout (ISO recommended).
- **NFR-5.4 (SHOULD)** Every screen states its data freshness (last import date, last price date, period status).
- **NFR-5.5 (SHOULD)** Destructive-looking actions require confirmation, but since nothing is truly deleted, confirmations should be light.

### NFR-6 — Maintainability
- **NFR-6.1 (MUST)** Business rules (categorization, transfer matching, runway formula) live in a testable layer separate from the web layer.
- **NFR-6.2 (MUST)** A seed dataset of synthetic transactions for testing, so the test suite never needs real financial data.
- **NFR-6.3 (MUST)** Tests cover: dedupe, transfer exclusion, split arithmetic, multi-currency conversion, snapshot correctness, runway calculation.
- **NFR-6.4 (SHOULD)** The system should be resumable after months of neglect — documentation lives in the repo, and the app itself tells you what state it's in.

---

## 7. Recommended technology

Deliberately boring, given weekly/monthly local use by a single technical user.

| Layer | Choice | Reason |
|---|---|---|
| Database | PostgreSQL (in Docker) | JSONB for raw rows, arrays for tags, real constraints, window functions for trends. SQLite is a defensible alternative if you want a single file. |
| Backend | One language you're fastest in — Python/FastAPI, Node/TypeScript, or Go | Single process, no queue, no workers |
| Frontend | Server-rendered HTML + a light interactivity layer (HTMX/Alpine), or a small SPA if you prefer | The value is in the model, not the UI; avoid a build pipeline you'll resent maintaining |
| Charts | Any simple charting library | Half a dozen charts total |
| Migrations | Whatever is native to your stack | Versioned, forward-only |
| File storage | Local filesystem, content-addressed by SHA-256 | Simple, deduplicating, backup-friendly |
| Scheduling | None. Everything runs on demand from the UI | Nothing needs to run while the app is off |

**Explicitly avoid:** microservices, message queues, Kubernetes, cloud databases, auth providers, ORMs that hide the SQL you'll want to write, and any dependency that requires a running account somewhere.

---

## 8. Delivery phases

Each phase ends in a usable system. Don't start a phase before the previous one's exit criteria are met.

### Phase 0 — Trust the data *(the phase that matters most)*
**Scope:** FR-1.1–1.4, FR-2 (import, dedupe, profiles, preview/commit), FR-3, FR-4 (transfers), FR-7 (reconciliation), FR-15.1, FR-16.2.
**Exit criteria:**
- Three months of real statements imported from every account
- Re-importing an overlapping file creates zero duplicates
- Every account reconciles to its stated balance to the cent
- Transfers are linked; no transfer leg appears as income or expense
- A restore from backup has been performed successfully at least once

### Phase 1 — Understand it
**Scope:** FR-5 (categories, rules, enrichment), FR-6 (review queue), FR-10.1–10.5, FR-14.2.
**Exit criteria:**
- Under 2% of transaction value uncategorized in any closed month
- Monthly report produced for the last 6 months
- A rule re-run across all history changes nothing that was set manually

### Phase 2 — Assets and net worth
**Scope:** FR-8 (holdings, gold, prices, FX), FR-9 (snapshots, net worth), FR-10.6–10.7.
**Exit criteria:**
- Gold and investment holdings tracked by quantity with dated prices
- Net worth snapshot exists for every closed month
- Historic net worth does not change when today's gold price changes
- Emergency runway in months displayed on the dashboard

### Phase 3 — Plan
**Scope:** FR-11 (goals), FR-12 (obligations, budgets, debt), FR-13 (forecast), FR-10.8–10.11.
**Exit criteria:**
- Adding a planned purchase produces a feasibility date and a runway impact
- A 12-month calendar of committed outflows exists
- Recurring subscriptions detected and reviewed at least once

### Phase 4 — Polish
**Scope:** FR-14.1 attachments, FR-15.4–15.5, FR-16.1/16.4/16.5, FR-10.12–10.13, quality-of-life improvements identified from actually using it for six months.

---

## 9. Open decisions

| # | Decision | Options | Recommendation |
|---|---|---|---|
| D1 | Buckets — virtual sub-accounts or a tag on balances? | Real bucket table vs. simple labels | Real bucket table, but only allocate at close time (FR-1.5), not per transaction |
| D2 | Base currency | AED / USD | Whichever your income and living costs are in; report a secondary currency as a display option |
| D3 | Cost basis method | Weighted average vs. FIFO | Weighted average — simpler, adequate for personal use |
| D4 | Cash spending | Track every cash transaction vs. periodic cash count | Periodic count with a `cash drift` adjustment (FR-7.7). Tracking cash daily contradicts the monthly cadence |
| D5 | Investment purchases in the expense report | Expense, transfer, or its own kind | Their own `investment` kind — not spending, not a pure transfer |
| D6 | Postgres vs. SQLite | — | Postgres if you'll use JSONB/arrays/window functions heavily; SQLite if a single portable file matters more |
| D7 | Where the review queue's "low confidence" threshold sits | — | Start strict, loosen as rules mature |
| D8 | How far back to backfill | 1 year vs. everything available | One clean year beats three messy ones; backfill later once the pipeline is proven |

---

## 10. Requirements summary

| Area | MUST | SHOULD | COULD |
|---|---|---|---|
| Accounts & buckets | 4 | 2 | 1 |
| Import | 11 | 3 | 1 |
| Transactions | 5 | 1 | 0 |
| Transfers | 6 | 1 | 0 |
| Enrichment | 8 | 4 | 0 |
| Review queue | 3 | 2 | 0 |
| Reconciliation | 5 | 2 | 0 |
| Assets & prices | 9 | 3 | 1 |
| Net worth | 3 | 2 | 0 |
| Reports | 7 | 4 | 2 |
| Goals | 5 | 3 | 1 |
| Obligations & budgets | 3 | 3 | 0 |
| Forecast | 1 | 3 | 1 |
| Attachments & audit | 2 | 1 | 0 |
| Browsing | 3 | 1 | 1 |
| Data lifecycle | 4 | 1 | 0 |
