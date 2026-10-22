# Compulsory Assignment 1 review guide

This repository has four lecture slices, and each folder is a separate Compose project. All four use the database, user, and password `mobility` on `localhost:5432`. Run commands from inside a lecture folder, not from the repository root. Because the slices share the port, stop the containers from the other lectures first.

Group members are Bo Djernis Schrøder (bodsch01) and Jeppe Kjelgaard Lynge Hansen (jephan01).

The submitted commit is `d2605e826e484cb49ec9af2f1cc0472044e9998f`.

For setup, follow the lecture README linked in the table below. To reset a slice, run these commands from its folder:

```bash
docker compose down -v
docker compose up -d
```

## Where to find the work

| Lecture                                                    | Contents                            | Evidence                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| ---------------------------------------------------------- | ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| [Lecture 1](mobilityticketing-lecture-1-starter/README.md) | model, workload map and queries     | [ER diagram](mobilityticketing-lecture-1-starter/docs/ERDiagram.png), [schema](mobilityticketing-lecture-1-starter/database/postgres/001_relational_baseline.sql), [queries](mobilityticketing-lecture-1-starter/database/postgres/003_queries.sql.example), [dossier](mobilityticketing-lecture-1-starter/docs/dossier.md)                                                                                                                                                          |
| [Lecture 2](mobilityticketing-lecture-2-starter/README.md) | constraints and tests               | [migration](mobilityticketing-lecture-2-starter/database/postgres/migrations/011_ticketing_integrity.sql), [rejected writes](mobilityticketing-lecture-2-starter/database/postgres/experiments/constraints_should_fail.sql), [accepted writes](mobilityticketing-lecture-2-starter/database/postgres/experiments/constraints_should_succeed.sql), [integrity map](mobilityticketing-lecture-2-starter/docs/integrity-map.md)                                                         |
| [Lecture 3](mobilityticketing-lecture-3-starter/README.md) | reporting experiment and comparison | [query](mobilityticketing-lecture-3-starter/database/postgres/queries/base_revenue.sql), [function](mobilityticketing-lecture-3-starter/database/postgres/migrations/020_reporting_function.sql), [trigger](mobilityticketing-lecture-3-starter/database/postgres/migrations/021_daily_revenue_trigger.sql), [view](mobilityticketing-lecture-3-starter/database/postgres/migrations/022_daily_captured_revenue.sql), [results](mobilityticketing-lecture-3-starter/docs/results.md) |
| [Lecture 4](mobilityticketing-lecture-4-starter/README.md) | migration stages and verification   | [expand](mobilityticketing-lecture-4-starter/database/postgres/migrations/030_expand_product_identity.sql), [backfill](mobilityticketing-lecture-4-starter/database/postgres/migrations/031_backfill_ticket_product.sql), [evidence](mobilityticketing-lecture-4-starter/docs/evidence/lecture04/README.md)                                                                                                                                                                          |

## Two decisions worth discussing

### Lecture 1

**A stop appears once on a route.** The primary key on `route_stops` is `(route_id, stop_id)`. The alternative was `(route_id, stop_sequence)`, which keeps the order and lets the same stop appear twice. The timetable workloads in this slice only need an ordered list of stops, and none of the current routes loop.

Evidence is in [001_relational_baseline.sql](mobilityticketing-lecture-1-starter/database/postgres/001_relational_baseline.sql) and [docs/dossier.md](mobilityticketing-lecture-1-starter/docs/dossier.md).

**The count report includes routes with no trips.** Query 3 uses a `LEFT JOIN` from `routes` to `trips` and puts the service date in the join condition. An `INNER JOIN` would hide routes that have no trips on that date. Route maintenance needs every route, including days with zero trips.

Evidence is in [003_queries.sql.example](mobilityticketing-lecture-1-starter/database/postgres/003_queries.sql.example).

### Lecture 2

**A validation must match both the ticket id and the ticket code.** `validations` has a composite foreign key on `(ticket_id, ticket_code)`. The alternative was a foreign key on `ticket_id` alone. A gate scan can carry both values, and the composite key rejects a write that combines the id of one ticket with the code of another.

Evidence is in [011_ticketing_integrity.sql](mobilityticketing-lecture-2-starter/database/postgres/migrations/011_ticketing_integrity.sql) and [docs/integrity-map.md](mobilityticketing-lecture-2-starter/docs/integrity-map.md).

**Gateway references are unique in our database, and every captured payment must have one.** `payments.external_payment_reference` has a `UNIQUE` constraint, and a check requires a reference on `Captured` rows. The alternative was to rely on application retries alone. The constraints give the database its own idempotency guard. They do not prove that a real payment gateway captured the payment.

Evidence is in the same migration and in [constraints_should_fail.sql](mobilityticketing-lecture-2-starter/database/postgres/experiments/constraints_should_fail.sql).

### Lecture 3

**Revenue reports read captured revenue from `payments`.** The correct figures come from the base query or from `captured_revenue_for_day`. The alternative was to use `daily_revenue_by_operator` as the report. The trigger behind that table only fires on inserts of captured payments, so it misses seed rows, status changes, refunds, and deletes. We accept that reports may lag, because a purchase write should not also have to update the report.

Evidence is in [docs/results.md](mobilityticketing-lecture-3-starter/docs/results.md), case 3 (query 122 / 3 vs trigger 36 / 1).

**The materialized view is an optional cache. `payments` stays the only source of revenue data.** `daily_captured_revenue` holds no data until `REFRESH` runs. The alternative was to build a complete trigger that also handles updates, deletes, and backfill. In the experiment, the view returned stale data until the next refresh, and one command rebuilds it from `payments`. A dashboard that is allowed to lag can live with that.

Evidence is in [022_daily_captured_revenue.sql](mobilityticketing-lecture-3-starter/database/postgres/migrations/022_daily_captured_revenue.sql) and in the decision record in [docs/results.md](mobilityticketing-lecture-3-starter/docs/results.md).

### Lecture 4

**The migration expands, backfills, and switches the applications before it removes `product_code`.** Stage 030 adds `product_id`, stage 031 fills it from `product_code`, and the legacy column goes last. The alternative was the one-step change in [unsafe_change.sql](mobilityticketing-lecture-4-starter/database/postgres/experiments/lecture04/unsafe_change.sql). That script drops the old link before the backfill and cannot make the new column required.

Evidence is in [docs/evidence/lecture04/README.md](mobilityticketing-lecture-4-starter/docs/evidence/lecture04/README.md).

**The new product foreign key starts as `NOT VALID`.** Stage 030 adds `tickets_product_id_fk` without checking existing rows. Old writers that leave `product_id` null keep working during the rollout. Validating at once would reject tickets created before the backfill. The scripts in [experiments/lecture04/](mobilityticketing-lecture-4-starter/database/postgres/experiments/lecture04/) run old and new readers and writers side by side.

Evidence is in [030_expand_product_identity.sql](mobilityticketing-lecture-4-starter/database/postgres/migrations/030_expand_product_identity.sql).

## One limitation or open question

The seat check does not stop two purchases from taking the last seat. `trips_reserved_seats_valid` only checks the value stored in a single `trips` row. Two transactions can both see a free seat and both increment `reserved_seats`. [docs/integrity-map.md](mobilityticketing-lecture-2-starter/docs/integrity-map.md) lists this as Issue 1.

The next step is to reserve the seat inside one transaction. Either lock the row with `SELECT ... FOR UPDATE`, or use a conditional update:

```sql
UPDATE trips
SET reserved_seats = reserved_seats + 1
WHERE id = ... AND reserved_seats < capacity;
```

Then run two overlapping purchases against a trip with one seat left.
