# Design decision

## Table sketch

| Table | Important columns | Responsibility |
| --- | --- | --- |
| `routes` | `id` PK, `short_name` | Current route identity and display name. |
| `trips` | `id` PK, `route_id` FK, `scheduled_departure_utc`, `status` | Current, authoritative trip state. A cancellation changes `status`; it does not delete the row. |
| `bookings` | `request_id` PK, `trip_id` FK, `booked_at_utc` | Idempotency record and original booking receipt. |
| `published_plans` | `id` PK, `from_utc`, `to_utc`, `published_at_utc` | Immutable publication identity, window and publication time. |
| `published_plan_routes` | `plan_id` FK, `route_id`, `route_name`, `scheduled_trip_count`, `next_departure_utc`; PK (`plan_id`, `route_id`) | Immutable copies of every displayed route row. It deliberately has no FK to the live route or trip rows. |

`published_plans` owns its detail rows with `ON DELETE CASCADE`. The application never updates a published header or detail. Counts are non-negative, plan windows are positive and no longer than 24 hours, and all timestamps use `timestamptz`.

## Command and query paths

```text
ReadOverview(window)
  -> validate window before database access
  -> one no-tracking projection: routes + aggregate scheduled trips in [from, to)
  -> ORDER BY route id -> RouteOverviewDto list

CancelTrip(tripId)
  -> validate id
  -> BEGIN transaction -> lock trip row FOR UPDATE
  -> missing / already cancelled, or set trips.status = 'Cancelled'
  -> COMMIT

RenameRoute(routeId, name)
  -> validate id and trimmed name
  -> BEGIN transaction -> find route -> set routes.short_name -> COMMIT

BookTrip(tripId, requestId)
  -> validate both ids
  -> BEGIN transaction + advisory lock "booking:{requestId}"
  -> read bookings by request_id
     -> same trip: return the stored receipt
     -> another trip: RequestConflict
  -> lock current trip row FOR UPDATE
     -> missing: NotFound; cancelled: Cancelled
  -> insert booking with Clock timestamp -> COMMIT

PublishSnapshot(planId, window)
  -> validate id and window before database access
  -> BEGIN transaction + advisory lock "plan:{planId}"
  -> read published_plans by id
     -> same window: AlreadyPublished
     -> another window: SnapshotConflictException
  -> run the live projection inside the transaction
  -> insert one header and all detail rows -> BeforePublishCommit hook -> COMMIT

ReadSnapshot(planId)
  -> validate id
  -> one no-tracking LEFT JOIN from header to details, ordered by route id
  -> missing header: SnapshotNotFoundException; otherwise assemble PublishedSnapshot
```

The booking request lock makes retries for one request ID serial. The trip row lock is the shared authority between booking and cancellation: whichever transaction locks it first determines whether that booking can be created. A retry checks `bookings` before current trip status, so it still returns the original receipt after cancellation. The plan-ID lock similarly serializes publication retries, while the surrounding transaction makes the header and all displayed rows atomic.

## Live-query loading choice

| Approach | Database reads | Data and objects loaded |
| --- | ---: | --- |
| Lazy baseline | `1 + route count` | Full `Route` entities, then full `Trip` entities for each route; 5 reads for 4 routes and 201 for 200 routes. |
| Eager `Include` | 1 (or 2 with a split query) | Every mapped route and matching trip column, repeated route values in a single joined result, plus `Route`/`Trip` objects before in-memory aggregation. |
| DTO projection (chosen) | 1 | Only route ID, route name, SQL `COUNT` and SQL `MIN`; no source entities and one result row per route. |

A filtered `Include` could avoid trips outside the requested window, but it still materializes objects and transfers trip-level rows that the result does not expose. Direct projection lets PostgreSQL aggregate before transfer and naturally retains routes with zero matching trips through correlated aggregates or a left join.

## Comparison and choice

Design A calculates the live board from `routes` and `trips` for every requested window, projecting directly to four DTO columns. Cancellation updates only `trips.status`; renaming updates only `routes.short_name`. The indexed trip source remains authoritative, arbitrary windows need no preparation, and publishing copies the projected values into immutable plan header/detail rows. Design B maintains a current-summary table. Reads are simple, but each cancellation and rename must synchronously repair derived rows. Because the API accepts arbitrary windows, the summary must either be rebuilt for every new window or become a cache keyed by route and window, multiplying storage, invalidation rules and race conditions. A materialized view has the same fixed-query and refresh-timing problem. Design A is chosen: it meets the one-read target with database-side `COUNT`/`MIN`, avoids duplicated current state, and isolates deliberate duplication to historical plans. Booking rules remain commands over current `trips`, never the board or a published plan. Booking and publication each run in one transaction, with stable advisory keys making retries deterministic.
