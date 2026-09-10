# Lab results: Where should reporting logic execute?

Authority for this experiment: `payments`. Stored reporting objects are derived.

SQL object definitions:

- Query: `database/postgres/queries/base_revenue.sql`
- Function: `database/postgres/migrations/020_reporting_function.sql` (`captured_revenue_for_day`)
- Trigger summary: `database/postgres/migrations/021_daily_revenue_trigger.sql` (`daily_revenue_by_operator`, `AFTER INSERT` only)
- Materialized view: `database/postgres/migrations/022_daily_captured_revenue.sql` (`daily_captured_revenue`, `WITH NO DATA`)

The trigger was left incomplete, as supplied. Behaviour was documented before adding more branches.

## 1. Reference query

```bash
docker compose exec -T postgres psql -U mobility -d mobility < database/postgres/queries/base_revenue.sql
```

Seeded captured payments, before any test writes:

```text
 operator_id | revenue_date | captured_amount | captured_payments
-------------+--------------+-----------------+-------------------
 OP-BUS      | 2026-04-29   |           36.00 |                 1
 OP-METRO    | 2026-04-29   |           36.00 |                 1
(2 rows)
```

Matches `PAYMENT-1` (OP-METRO / TICKET-1) and `PAYMENT-2` (OP-BUS / TICKET-2).

## 2. SQL function

Applied `020_reporting_function.sql`. Same read logic as the query; no stored aggregate.

```bash
docker compose exec -T postgres psql -U mobility -d mobility -c "select * from captured_revenue_for_day('OP-BUS', '2026-04-29');"
```

```text
 captured_amount | captured_payments
-----------------+-------------------
           36.00 |                 1
(1 row)
```

Difference: one operator/date, not the full grouped report.

## 3. Trigger-maintained summary

Applied `021_daily_revenue_trigger.sql`. `AFTER INSERT` only. No backfill of seed payments.

```text
 operator_id | revenue_date | captured_amount | captured_payments
-------------+--------------+-----------------+-------------------
(0 rows)
```

## 4. Materialized view

Applied `022_daily_captured_revenue.sql`. Created `WITH NO DATA`. `REFRESH` was not run until after the test cases.

```text
ERROR:  materialized view "daily_captured_revenue" has not been populated
HINT:  Use the REFRESH MATERIALIZED VIEW command.
```

## Test cases

Reads after each write: base query, `captured_revenue_for_day('OP-METRO', '2026-04-29')`, `daily_revenue_by_operator`, `daily_captured_revenue`. The view stayed unpopulated until the explicit refresh after case 6.

### Case 1 — captured insert (`PAY-CASE-CAPTURED`, 36 DKK, OP-METRO)

`INSERT 0 1`


| Source        | Result                                           |
| ------------- | ------------------------------------------------ |
| Query         | OP-BUS 36 / 1, OP-METRO **72 / 2**               |
| Function      | **72 / 2**                                       |
| Trigger table | OP-METRO **36 / 1** (new row only; seed missing) |
| View          | not populated                                    |


### Case 2 — failed insert (`PAY-CASE-FAILED`, 50 DKK)

`INSERT 0 1`

No captured report moved. Query still OP-METRO 72 / 2. Trigger still 36 / 1. View still unpopulated. The trigger skipped the row because `status` was not `Captured`.

### Case 3 — Failed → Captured

`UPDATE 1`


| Source        | Result                                 |
| ------------- | -------------------------------------- |
| Query         | OP-BUS 36 / 1, OP-METRO **122 / 3**    |
| Function      | **122 / 3**                            |
| Trigger table | still **36 / 1** (no `UPDATE` trigger) |
| View          | not populated                          |


### Case 4 — Captured → Refunded (`PAY-CASE-CAPTURED`)

`UPDATE 1`


| Source        | Result                                   |
| ------------- | ---------------------------------------- |
| Query         | OP-BUS 36 / 1, OP-METRO **86 / 2**       |
| Function      | **86 / 2**                               |
| Trigger table | still **36 / 1** (refund not subtracted) |
| View          | not populated                            |


### Case 5 — delete `PAY-CASE-FAILED`

`DELETE 1`


| Source        | Result                             |
| ------------- | ---------------------------------- |
| Query         | OP-BUS 36 / 1, OP-METRO **36 / 1** |
| Function      | **36 / 1**                         |
| Trigger table | still **36 / 1**                   |
| View          | not populated                      |


The OP-METRO 36 / 1 figures look the same but are not the same payment. Query/function count seed `PAYMENT-1`. The trigger still counts the refunded `PAY-CASE-CAPTURED`. OP-BUS is still missing from the trigger table.

### Case 6 — duplicate `external_payment_reference` (`gateway-capture-0001`)

`INSERT 0 1` — the write succeeded. This slice has no unique constraint on the gateway reference.


| Source        | After insert                          | After `REFRESH MATERIALIZED VIEW`  |
| ------------- | ------------------------------------- | ---------------------------------- |
| Query         | OP-BUS 36 / 1, OP-METRO **72 / 2**    | unchanged                          |
| Function      | **72 / 2**                            | unchanged                          |
| Trigger table | OP-METRO **72 / 2** (still no OP-BUS) | unchanged                          |
| View          | not populated                         | OP-BUS 36 / 1, OP-METRO **72 / 2** |


The trigger 72 / 2 is again coincidental: refunded `PAY-CASE-CAPTURED` + duplicate. The query 72 / 2 is seed `PAYMENT-1` + duplicate.

## Disagreement examples

1. After applying 021/022, before any test write: query/function show 36+36; trigger table is empty; view is unreadable.
2. Case 3 is the clearest live disagreement: query/function 122 / 3 vs trigger 36 / 1 after a status correction the trigger cannot see.
3. After refresh, the view matches the query. The trigger still disagrees: missing OP-BUS, missing seed, still includes a refunded payment.

## Side-effect trace — `INSERT PAY-CASE-CAPTURED`

Write:

```sql
insert into payments (
    id, user_id, ticket_id, external_payment_reference,
    amount, currency, status, created_utc
) values (
    'PAY-CASE-CAPTURED', 'USER-1', 'TICKET-1', 'gateway-case-captured',
    36, 'DKK', 'Captured', '2026-04-29 10:00:00+00'
);
```

1. **Constraints / references.** `payments.id` is the only primary key in this slice. Ticketing tables were created without foreign keys. The insert does not prove that `ticket_id` or `user_id` exist. The trigger looks up operator via ticket → trip → route. If that lookup returned null, the insert into `daily_revenue_by_operator` would fail the `references operators(id)` check and roll back the payment write.
2. **Trigger.** `payments_daily_revenue_after_insert` ran because status was `Captured`.
3. **Summary write.** One insert into `daily_revenue_by_operator` for OP-METRO / 2026-04-29 with amount 36 and count 1. No `ON CONFLICT` update, because the table was empty.
4. **Rows touched.** One new `payments` row. One new summary row. Seed payments were not read into the summary. Function and view issued no writes.
5. **Commit / rollback.** The payment insert and the summary insert committed together (`INSERT 0 1`). A later trigger failure would roll back the payment as well.
6. **When each report became current.** Query and function: immediately after commit. Trigger table: immediately, but only for this insert. View: not until the later `REFRESH`.
7. **What the application can observe.** The insert succeeded. A reader of `payments` or the function sees OP-METRO 72. A reader of the summary sees only 36. A reader of the view gets an error until refresh.

## Responsibility matrix


| Approach          | Authority                                          | Freshness                                              | Write cost                              | Read cost                        | Hidden side effects                                                                     | Rebuild path                                                                          | Operational complexity                      |
| ----------------- | -------------------------------------------------- | ------------------------------------------------------ | --------------------------------------- | -------------------------------- | --------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------- | ------------------------------------------- |
| Direct query      | `payments`                                         | Current at read                                        | None on writes                          | Full aggregate + joins           | None                                                                                    | Re-run the query                                                                      | Low                                         |
| Function          | `payments`                                         | Current at read                                        | None on writes                          | Same as query, one operator/date | None                                                                                    | Re-run / replace the function                                                         | Low                                         |
| Materialized view | Snapshot of the query                              | Stale until `REFRESH`                                  | None on payment writes; cost on refresh | Cheap after populate             | Stale reads if refresh is forgotten                                                     | `REFRESH MATERIALIZED VIEW`; recreate from `payments`                                 | Medium: schedule, monitor, empty-view error |
| Trigger table     | Derived increment; **not** rebuilt from `payments` | Current only for captured inserts the trigger has seen | Extra write in the payment transaction  | Cheap                            | Summary write, possible rollback of the business write, missed updates/deletes/backfill | Must recompute from `payments`; incremental table cannot be trusted after corrections | High                                        |


Named rules used in the comparison:

- **Authority:** `payments` (and the joins needed to reach `operator_id`).
- **Freshness rule for this case:** operator revenue reports may lag; purchase and validation must not wait on reporting (MobilityTicketing design brief).
- **Rebuild path:** any stored copy must be reconstructible from `payments`. The incomplete trigger has no such path in the supplied code.

## Recommendation

Hybrid:

1. Keep `payments` as authority.
2. Use the SQL function (or the base query) as the correctness path for captured revenue.
3. Use the materialized view only if operators need a faster dashboard. Document a refresh schedule and treat it as a cache, not a second ledger.
4. Do not use the supplied trigger table as the reporting source of truth.

Why not the trigger for this case:

- Reporting may be stale; coupling a summary write to every payment increases write latency and failure scope.
- Corrections, refunds, deletes, and initial backfill were wrong until extra trigger branches exist. Even then, duplicate delivery and coincidental matching totals remain hard to observe.
- Recovery means rebuilding from `payments`. A function or `REFRESH` already does that. An incremental counter does not.

Out of scope, as the lab states: ticket-purchase concurrency and payment capture across an external gateway.

## Issue (for the issue register)

**Title:** Trigger-maintained daily revenue diverges from `payments` on corrections, deletes, backfill, and duplicate gateway references.

**Body:** `daily_revenue_by_operator` is maintained only on `AFTER INSERT` of `Captured` payments. Seeded captured payments are missing. `Failed → Captured`, `Captured → Refunded`, and `DELETE` do not update the summary. The same `external_payment_reference` can be inserted twice, and both the query and the trigger will count it. After some sequences the trigger totals can match the query while representing different payment rows. Rebuild from `payments` is not implemented.

## Decision record

- **Date:** 2026-09-09
- **Decision:** Daily captured revenue is read from `payments` via `captured_revenue_for_day` / the base query. A materialized view may be added as a refreshable cache. The trigger-maintained table is not the authority.
- **Status:** Proposed for this lecture slice.
- **Reason:** The experiment showed query/function tracking source data, the view staying stale until refresh, and the trigger both missing events and matching totals for the wrong rows. The case allows reporting latency. Purchase writes should not own reporting side effects.
- **Affected workloads:** reporting; payment writes must not wait on report maintenance.
- **Alternatives:** complete the trigger for update/delete/backfill; application-side aggregation; scheduled ETL into a warehouse table.
- **Consequences:** reports are correct at read (or after an explicit refresh). Dashboard freshness depends on the refresh policy. Duplicate gateway captures remain a constraint/workflow problem, not a reporting-layer fix.
- **Source:** `docs/lab.md`, this experiment, MobilityTicketing reporting workload.

