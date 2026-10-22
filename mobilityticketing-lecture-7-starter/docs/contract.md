# Application contract

The CLI and tests call the six methods in `ILectureApplication` through `StudentApplication`. Keep its constructor and method signatures. Choose your own command/query classes and snapshot storage.

## Live board

Return `RouteOverviewDto` rows ordered by route ID. Include every route, including routes with no departures. Count `Scheduled` trips in `[FromUtc, ToUtc)`. The earliest matching departure is nullable: an empty route returns `0 / null`.

Accept positive UTC intervals up to 24 hours. Both endpoints must be exact multiples of one microsecond (10 .NET ticks). Reject finer precision with `ArgumentException` before any database query or write; do not round or truncate the window. For example, `06:00:00.000001Z` is valid and `06:00:00.0000007Z` is invalid. This keeps query boundaries and publication retries consistent with PostgreSQL timestamp precision. The default is 23 October 2026, 06:00 to 07:00 UTC. After reset, it returns:

| Route | Name | Count | Next UTC |
| --- | --- | ---: | --- |
| R1 | City Loop | 2 | 06:00 |
| R2 | Campus Line | 2 | 06:10 |
| R3 | Airport Express | 0 | null |
| R4 | Harbour Shuttle | 0 | null |

Booking records do not change the number of departures. Cancelling a trip does.

The live read must use one or two executed database reads. Reading a published plan must use one read and load no `Route` or `Trip` entities. Both operations are read-only.

## Commands and saved plans

| Method | Required behavior |
| --- | --- |
| `ReadOverviewAsync(window)` | Return current values on the next successful read. |
| `CancelTripAsync(id)` | Return `Cancelled`, `AlreadyCancelled` or `NotFound`. Keep the trip row. |
| `RenameRouteAsync(id, name)` | Trim the name, reject blank names, and return `Renamed` or `NotFound`. |
| `BookTripAsync(trip, request)` | Save a receipt for a scheduled trip. Reject cancelled or missing trips. Repeating a successful request returns its original receipt, including after later cancellation. Reusing that request ID for another trip returns `RequestConflict`. |
| `PublishSnapshotAsync(id, window)` | Save every displayed value, the window and the publication time together. Repeating the ID/window returns `AlreadyPublished` and preserves the original. Reusing the ID for another window raises `SnapshotConflictException`. |
| `ReadSnapshotAsync(id)` | Return the original rows, window and time. Missing IDs raise `SnapshotNotFoundException`. |

Blank IDs and invalid windows raise `ArgumentException` without writes. Cancellation and renaming leave published plans unchanged. Use a new plan ID to capture later values.

## Storage and supplied helpers

The source tables `routes`, `trips` and `bookings` are provided in `lab07`. Define your snapshot storage in `03-read-model.sql`. It must survive a new application process. A header with detail rows or a JSON document can both fit the contract.

For EF mappings, `LectureDbContext` has a `ConfigureReadModels` partial method. You can implement it in another partial-class file. Use `StudentApplication.CreateContext()` for measured reads so the SQL and entity counters stay attached. EF's SQL-query APIs also work through that context.

`DatabaseSession.WriteAsync` opens a transaction, saves tracked changes and commits. It takes a lock key; calls with the same key wait for each other. Choose stable keys for booking requests and publication retries. A failed callback or hook rolls back the write.

`DatabaseSession.LockTripAsync` locks the trip row. Use it when booking or cancelling so those operations check and change the same current trip state.

Use the supplied `Clock` for receipt and publication times. PostgreSQL stores timestamp values to microsecond precision; a returned receipt must match the value read back on retry. Pass `Hooks.BeforePublishCommit` to the publication transaction. The tests use it to simulate a failure before commit.

## Other windows and tests

CLI timestamps need `Z` or an explicit UTC offset. For example:

```sh
docker compose run --rm app overview 2026-10-23T06:20:00Z 2026-10-23T07:00:00Z
docker compose run --rm app publish later 2026-10-23T07:00:00Z 2026-10-23T08:00:00Z
```

The acceptance checks cover time boundaries, empty routes, read-only queries, retries, booking/cancellation races, saved values, rollback and independent windows. Add at least four tests in `StudentTests.cs`, including a failure or concurrency case. Extend the supplied cases with sequences you think could go wrong, and explain what each test protects.
