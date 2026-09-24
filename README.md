# Compulsory Assignment 1 review guide

Four lecture slices live in this repository. Each folder is its own Compose project. The database, user, and password are `mobility`, on `localhost:5432`. Run commands inside the lecture folder, not from the repository root. Stop the other lecture containers first; they share that port.

**Submitted commit:** replace this line with the full hash of the commit handed in on Moodle.

**Setup and reset:** use the lecture README in the table. From that folder, run `docker compose down -v` and then `docker compose up -d`.

## Where to find the work

| Lecture | Contents | Evidence |
|---|---|---|
| [Lecture 1](mobilityticketing-lecture-1-starter/README.md) | model, workload map and queries | [ER diagram](mobilityticketing-lecture-1-starter/docs/ERDiagram.png), [schema](mobilityticketing-lecture-1-starter/database/postgres/001_relational_baseline.sql), [queries](mobilityticketing-lecture-1-starter/database/postgres/003_queries.sql.example), [dossier](mobilityticketing-lecture-1-starter/docs/dossier.md) |
| [Lecture 2](mobilityticketing-lecture-2-starter/README.md) | constraints and tests | [migration](mobilityticketing-lecture-2-starter/database/postgres/migrations/011_ticketing_integrity.sql), [rejected writes](mobilityticketing-lecture-2-starter/database/postgres/experiments/constraints_should_fail.sql), [accepted writes](mobilityticketing-lecture-2-starter/database/postgres/experiments/constraints_should_succeed.sql), [integrity map](mobilityticketing-lecture-2-starter/docs/integrity-map.md) |
| [Lecture 3](mobilityticketing-lecture-3-starter/README.md) | reporting experiment and comparison | [query](mobilityticketing-lecture-3-starter/database/postgres/queries/base_revenue.sql), [function](mobilityticketing-lecture-3-starter/database/postgres/migrations/020_reporting_function.sql), [trigger](mobilityticketing-lecture-3-starter/database/postgres/migrations/021_daily_revenue_trigger.sql), [view](mobilityticketing-lecture-3-starter/database/postgres/migrations/022_daily_captured_revenue.sql), [results](mobilityticketing-lecture-3-starter/docs/results.md) |
| [Lecture 4](mobilityticketing-lecture-4-starter/README.md) | migration stages and verification | [expand](mobilityticketing-lecture-4-starter/database/postgres/migrations/030_expand_product_identity.sql), [backfill](mobilityticketing-lecture-4-starter/database/postgres/migrations/031_backfill_ticket_product.sql), [evidence](mobilityticketing-lecture-4-starter/docs/evidence/lecture04/README.md) |

## Two decisions worth discussing

### Lecture 1

**A stop appears once on a route.** Primary key `(route_id, stop_id)` on `route_stops`. The alternative is `(route_id, stop_sequence)`, which would allow the same stop twice while still keeping order. This slice only needs an ordered stop list for the timetable workloads, and a loop is not part of the current routes. Evidence: [`001_relational_baseline.sql`](mobilityticketing-lecture-1-starter/database/postgres/001_relational_baseline.sql), [`docs/dossier.md`](mobilityticketing-lecture-1-starter/docs/dossier.md).

**Routes with no trips still appear in the count report.** Query 3 uses `LEFT JOIN` from `routes` to `trips` with the service date in the join condition. The alternative is `INNER JOIN`, which would hide routes that have no trips on that date. Route maintenance needs the full route list, including zero-trip days. Evidence: [`003_queries.sql.example`](mobilityticketing-lecture-1-starter/database/postgres/003_queries.sql.example).

### Lecture 2

**Validations must match both ticket id and ticket code.** Composite foreign key `(ticket_id, ticket_code)` on `validations`. The alternative is a foreign key on `ticket_id` only. A gate scan can carry both values; the composite key stops a write from mixing the id of one ticket with the code of another. Evidence: [`011_ticketing_integrity.sql`](mobilityticketing-lecture-2-starter/database/postgres/migrations/011_ticketing_integrity.sql), [`docs/integrity-map.md`](mobilityticketing-lecture-2-starter/docs/integrity-map.md).

**Gateway references are unique locally, and captured payments must carry one.** `UNIQUE` on `payments.external_payment_reference` plus a check that `Captured` rows have a reference. The alternative is to rely on application retries only. That gives the database a local idempotency guard without pretending it proves a real gateway capture. Evidence: same migration and [`constraints_should_fail.sql`](mobilityticketing-lecture-2-starter/database/postgres/experiments/constraints_should_fail.sql).

### Lecture 3

**Captured revenue is read from `payments`.** The base query or `captured_revenue_for_day` is the correctness path. The alternative was to treat `daily_revenue_by_operator` as the report. The trigger runs on captured inserts only, so it misses seed rows, status changes, refunds, and deletes. Reporting may lag; a purchase write should not also maintain the report. Evidence: [`docs/results.md`](mobilityticketing-lecture-3-starter/docs/results.md), case 3 (query 122 / 3 vs trigger 36 / 1).

**A materialized view is an optional cache, not a second ledger.** `daily_captured_revenue` is populated only after `REFRESH`. The alternative is to invest in a complete trigger that handles updates, deletes, and backfill. The experiment showed stale reads until refresh, but rebuild is one command from `payments`. That fits a dashboard that may lag. Evidence: [`022_daily_captured_revenue.sql`](mobilityticketing-lecture-3-starter/database/postgres/migrations/022_daily_captured_revenue.sql), decision record in [`docs/results.md`](mobilityticketing-lecture-3-starter/docs/results.md).

### Lecture 4

**Expand, backfill, and switch applications before removing `product_code`.** Stage 030 adds `product_id`, stage 031 backfills from `product_code`, and legacy removal comes last. The alternative is the one-step change in [`unsafe_change.sql`](mobilityticketing-lecture-4-starter/database/postgres/experiments/lecture04/unsafe_change.sql), which drops the old link before backfill and cannot make the new column required. Evidence: [`docs/evidence/lecture04/README.md`](mobilityticketing-lecture-4-starter/docs/evidence/lecture04/README.md).

**The new product foreign key starts as `NOT VALID`.** `tickets_product_id_fk` is added without validating existing rows, so old writers that leave `product_id` null keep working during rollout. The alternative is to validate immediately, which would reject pre-backfill tickets. Mixed-version readers and writers are exercised in [`experiments/lecture04/`](mobilityticketing-lecture-4-starter/database/postgres/experiments/lecture04/). Evidence: [`030_expand_product_identity.sql`](mobilityticketing-lecture-4-starter/database/postgres/migrations/030_expand_product_identity.sql).

## One limitation or open question

The seat check does not stop two purchases from taking the last seat. `trips_reserved_seats_valid` only checks the value stored in one `trips` row. Two transactions can both read spare capacity and both increment `reserved_seats`. That limit is Issue 1 in [`docs/integrity-map.md`](mobilityticketing-lecture-2-starter/docs/integrity-map.md). Next check: reserve the seat in one transaction with `SELECT ... FOR UPDATE`, or with `UPDATE trips SET reserved_seats = reserved_seats + 1 WHERE id = ... AND reserved_seats < capacity`, then run two overlapping purchases against one remaining seat.
