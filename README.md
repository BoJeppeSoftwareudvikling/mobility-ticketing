# MobilityTicketing · Compulsory Assignment 2

Bo Djernis Schrøder (bodsch01) and Jeppe Kjelgaard Lynge Hansen (jephan01)

For review, check out the full commit SHA listed with the [repository link](https://github.com/BoJeppeSoftwareudvikling/mobility-ticketing) in the hand-in. Run `git rev-parse HEAD` to verify the checkout. The [assignment 1 guide](https://github.com/BoJeppeSoftwareudvikling/mobility-ticketing/blob/5f8ee429c941f0c880419067b5cb1a17cb54e455/README.md) covers lectures 1 to 4.

## Setup and reset

Use Docker with Compose and a Bash shell. Lecture 6 also uses the .NET 8 SDK on the host. Run each block from the linked lecture folder. Run one lab at a time and finish with `docker compose down` in that folder. Lectures 5 and 6 both publish MongoDB on port 27017. Each block below starts or resets its own lab fixture.

### 1. Journey search in MongoDB

Each document groups departures for a city, route, directed stop pair and service date. A passenger can read the fare and matching departures together. The same trip can appear under several stop pairs, so cancelling it requires updating every copy. The search applies status and both time bounds to the same array element. [Lecture 5](mobilityticketing-lecture-5-starter/README.md) · [Document model and fixture](mobilityticketing-lecture-5-starter/database/mongodb/lecture05/setup.js) · [Search](mobilityticketing-lecture-5-starter/database/mongodb/lecture05/search.js) · [Cancellation](mobilityticketing-lecture-5-starter/database/mongodb/lecture05/updates.js)

```bash
docker compose up -d --wait mongo
for script in reset search updates search; do
  docker compose exec -T mongo mongosh --quiet --file "/scripts/lecture05/$script.js"
done
```

The first search returned only `LAB05-T-OK` at 06:20 UTC. It excluded the cancelled 06:10 departure and the departure at the exclusive 07:00 boundary. The update matched and changed two documents. Repeating the search returned `[]`.

### 2. Journey-search cache in Redis

The cache key includes city, directi	
￼Become member of
◄ Compulsory Assignment #2 Hand-in
Jump to activityon and both UTC window bounds. A repeated morning search can reuse the result for 20 seconds by default. In the cancellation experiment we used 300 seconds so we could inspect the stale entry. MongoDB returned an empty result while Redis still returned the cancelled trip. Deleting the exact key after the update made the next search read MongoDB. When Redis was unavailable the search used MongoDB. When both failed it returned a search error. [Lecture 6](mobilityticketing-lecture-6-starter/README.md) · [Cache implementation](mobilityticketing-lecture-6-starter/database/redis/lecture06/CacheLab/CachedJourneySearch.cs) · [Tests](mobilityticketing-lecture-6-starter/database/redis/lecture06/CacheLab.Tests) · [Recorded hits, invalidation and outages](mobilityticketing-lecture-6-starter/docs/evidence/lecture06/README.md)

```bash
docker compose up -d --wait mongo redis
docker compose exec -T mongo mongosh --quiet --file /scripts/lecture05/reset.js
key='search:LAB06:v2:CPH:STOP-NORREPORT:STOP-AIRPORT:2026-10-02T06%3A00%3A00.0000000Z:2026-10-02T07%3A00%3A00.0000000Z'
docker compose exec -T redis redis-cli DEL "$key"
search() { CACHE_EXPIRY_SECONDS=300 dotnet run --project database/redis/lecture06/CacheLab -- morning; }
search
docker compose exec -T mongo mongosh --quiet < ../mobilityticketing-lecture-5-starter/database/mongodb/lecture05/updates.js
search
docker compose exec -T redis redis-cli DEL "$key"
search
dotnet test database/redis/lecture06/CacheLab.Tests
```

### 3. Route overview in PostgreSQL and EF Core

**Decision 1. Compute the live board in one query.** Dispatchers need one row per route, including routes with no scheduled departure in the requested window. The projection asks PostgreSQL for the count and earliest time. Lazy loading fetched every trip for each route before calculating those values. On four routes the projection reduced five reads to one. On 200 routes it reduced 201 reads to one and loaded zero trip entities instead of 100,000. Both strategies returned identical rows on each fixture. The projection recalculates aggregates on each read. A stored summary would need refreshing after changes and would have to support different time windows. [Lecture 7 setup and tests](mobilityticketing-lecture-7-starter/README.md) · [Live query](mobilityticketing-lecture-7-starter/src/MobilityTicketing.Lecture07/Queries/RouteOverviewQuery.cs) · [Measurements and SQL logs](mobilityticketing-lecture-7-starter/docs/evidence/lecture07/README.md) · [Design alternatives](mobilityticketing-lecture-7-starter/docs/design.md)

```bash
docker compose up -d --wait postgres
docker compose build app tests
for step in reset baseline overview scale baseline overview; do
  docker compose run --rm app "$step"
done
docker compose run --rm tests
```

### 4. Departure-search query and index

**Decision 2. Keep the half-open UTC range and the partial index on scheduled trips.** Passengers request the first 20 departures for a route and day, ordered by departure time and ID. The range rewrite alone still scanned all 120,008 trips. A route-only index helped the quieter R-7 but PostgreSQL skipped it for busy R-0. The chosen index supplies the order and lets the scan stop at 20. Median times fell from 6.610 to 0.131 ms for R-7 and from 11.906 to 0.134 ms for R-0. It costs 4,408 kB and adds index maintenance on inserts, rescheduling and cancellations. [Lecture 8 measurement procedure](mobilityticketing-lecture-8-starter/README.md) · [Query](mobilityticketing-lecture-8-starter/queries/candidate.sql) · [Index](mobilityticketing-lecture-8-starter/queries/indexes.sql) · [Plans](mobilityticketing-lecture-8-starter/docs) · [Five-run medians and correctness checks](mobilityticketing-lecture-8-starter/docs/evidence.md)

```bash
docker compose up -d --wait postgres
lab() { docker compose exec -T postgres bash /lab/run.sh "$@"; }
lab reset
for route in R-7 R-0; do lab measure baseline "$route"; lab measure candidate "$route"; done
docker compose exec -T postgres psql -U mobility -d mobility_l08 -c 'CREATE INDEX trips_route_id_idx ON lab08.trips (route_id)'
for route in R-7 R-0; do lab measure candidate "$route"; done
lab indexes
lab check
for route in R-7 R-0; do lab measure candidate "$route"; done
```

### 5. Two passengers request the last seat

The unsafe read-then-write produced two bookings while the counter showed one reserved seat. The booking function increments the counter only when the trip is scheduled and capacity remains. The booking insert shares the transaction, so rollback releases the seat. A request-ID lock also allows a retry to return the original booking ID. [Lecture 9 setup and session commands](mobilityticketing-lecture-9-starter/README.md) · [Booking function](mobilityticketing-lecture-9-starter/sql/book.sql) · [Checks](mobilityticketing-lecture-9-starter/sql/check.sql) · [Recorded concurrency and rollback results](mobilityticketing-lecture-9-starter/LECTURE-9-RESULTS.md)

```bash
docker compose up -d --wait postgres
docker compose exec -T postgres bash /lab/run.sh reset
docker compose exec -T postgres bash /lab/run.sh check
```

Open two psql sessions as shown in the lecture setup. In A, run the following and leave the transaction open.

```sql
BEGIN;
SELECT lab09.book('review-A', 'passenger-A', 'T-LAST');
```

In B, run the competing request. It waits for A.

```sql
SELECT lab09.book('review-B', 'passenger-B', 'T-LAST');
```

Run `COMMIT;` in A. B reports that the trip is full. Run `\i /lab/sql/inspect.sql` in A to verify one booking and one reserved seat. Reset and repeat with `ROLLBACK;` in A to see B take the seat.

## Open question

Can a search put a cancelled trip back in Redis after invalidation? The cancellation experiment checked an entry that already existed. Next we would pause a cache miss after it reads MongoDB, cancel the trip, delete the key, then resume the paused write. A stale hit on the next search would demonstrate the race and give us a repeatable test for version-based invalidation. [Cache experiment and race description](mobilityticketing-lecture-6-starter/docs/evidence/lecture06/README.md)
