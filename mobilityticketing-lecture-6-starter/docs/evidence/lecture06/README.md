# Lecture 6 Redis cache evidence

Observed on 8 October 2026 with the MongoDB Lecture 5 fixture reset before testing. The cache expiry was configured to 20 seconds.

## Cache keys

Morning:

```text
search:LAB06:v2:CPH:STOP-NORREPORT:STOP-AIRPORT:2026-10-02T06%3A00%3A00.0000000Z:2026-10-02T07%3A00%3A00.0000000Z
```

Evening:

```text
search:LAB06:v2:CPH:STOP-NORREPORT:STOP-AIRPORT:2026-10-02T18%3A00%3A00.0000000Z:2026-10-02T19%3A00%3A00.0000000Z
```

## Test A: Repeat a search

The exact morning key was deleted before the test.

| Request | Cache status | Data source | Returned trip IDs | MongoDB calls | Elapsed time |
| --- | --- | --- | --- | ---: | ---: |
| First morning request | `miss` | MongoDB | `LAB05-T-OK` | 1 | 293 ms |
| Second morning request | `hit` | Redis | `LAB05-T-OK` | 0 | 235 ms |

The second process made no MongoDB calls and returned the same journey from Redis.

## Test B: Cache an empty result

The exact evening key was deleted before the test.

| Request | Cache status | Data source | Returned trip IDs | MongoDB calls | Elapsed time |
| --- | --- | --- | --- | ---: | ---: |
| First evening request | `miss` | MongoDB | `[]` | 1 | 311 ms |
| Second evening request | `hit` | Redis | `[]` | 0 | 218 ms |

The empty result was cached as valid data. The second process returned it without calling MongoDB.

## Test C: Expire a result

The morning result was cached, then its expiry was changed:

```sh
docker compose exec -T redis redis-cli EXPIRE 'search:LAB06:v2:CPH:STOP-NORREPORT:STOP-AIRPORT:2026-10-02T06%3A00%3A00.0000000Z:2026-10-02T07%3A00%3A00.0000000Z' 1
```

`EXPIRE` returned `1`. After waiting, `TTL` returned `-2`.

| Request | Cache status | Data source | Returned trip IDs | MongoDB calls | Elapsed time |
| --- | --- | --- | --- | ---: | ---: |
| Morning request after expiry | `miss` | MongoDB | `LAB05-T-OK` | 1 | 254 ms |

The refreshed entry had a TTL of 17 seconds when checked, confirming that the result was saved again with the configured 20-second expiry.

## Test D: Store invalid values

Each value was stored under the exact morning key with a 20-second expiry. Each run printed `invalid`, fetched the valid result from MongoDB, and replaced the bad value.

| Cached value | Cache status | Data source | Returned trip IDs | MongoDB calls | Elapsed time |
| --- | --- | --- | --- | ---: | ---: |
| `not-json` | `invalid` | MongoDB | `LAB05-T-OK` | 1 | 316 ms |
| JSON with `schemaVersion: 1` | `invalid` | MongoDB | `LAB05-T-OK` | 1 | 316 ms |
| JSON missing `items` | `invalid` | MongoDB | `LAB05-T-OK` | 1 | 326 ms |

The readable invalid values used were:

```json
{"schemaVersion":1,"cachedAtUtc":"2026-10-08T08:30:00Z","items":[]}
```

```json
{"schemaVersion":2,"cachedAtUtc":"2026-10-08T08:30:00Z"}
```

## Test 5: Cancel a cached departure

The MongoDB fixture was reset and the exact morning key was deleted. The morning search was then run with a temporary five-minute expiry:

```sh
CACHE_EXPIRY_SECONDS=300 dotnet run --project database/redis/lecture06/CacheLab -- morning
```

The first search missed the cache, returned `LAB05-T-OK` from MongoDB, and made one MongoDB call in 314 ms. Redis reported a remaining TTL of 296 seconds when checked.

The Lecture 5 cancellation update then set every matching `LAB05-T-OK` departure to `Cancelled`. MongoDB reported two matched and two modified documents.

| Search | Cache status | Data source | Returned trip IDs | MongoDB calls | Elapsed time |
| --- | --- | --- | --- | ---: | ---: |
| Uncached after cancellation | Not applicable | MongoDB | `[]` | Not measured by the script | Not measured by the script |
| Cached before deletion | `hit` | Redis | `LAB05-T-OK` | 0 | 288 ms |
| Cached after exact-key deletion | `miss` | MongoDB | `[]` | 1 | 302 ms |

The `DEL` command returned `1`, confirming that the exact cached value was removed. The next cached search fetched the current MongoDB state and cached the empty result.

### Passenger-visible behavior

Before deletion, the passenger could still see and select `LAB05-T-OK` even though MongoDB marked it cancelled. Redis contained a validly formatted but stale snapshot. After deletion, the next search missed Redis, read MongoDB, and showed no matching departure.

If MongoDB had not received or completed the cancellation before the key was deleted, the next cache miss could read the still-scheduled departure and cache it again. Invalidation therefore needs to happen after the authoritative database update succeeds.

Quiz question 6 describes the remaining stale-write race. A search can miss the cache and read the old MongoDB result, then remain in progress. Another operation can cancel the departure and delete the key. The first search can subsequently finish and write its old result after the deletion, recreating the stale cache entry. `DEL` removes the value that exists at that moment; it does not cancel or order writes from requests already in progress. This experiment does not implement a solution to that race.

After the experiment, the MongoDB fixture was reset, the morning key was deleted, and the search was run without `CACHE_EXPIRY_SECONDS`. The runner printed a 20-second expiry, returned `LAB05-T-OK`, and Redis reported a TTL of 16 seconds when checked.

## Test 6: Service outages

### Test A: Redis unavailable

Only Redis was stopped. The morning search printed `cache unavailable`, fell back to MongoDB, and returned `LAB05-T-OK` rather than failing or returning an empty list.

| Cache status | Data source | Returned trip IDs | MongoDB calls | Elapsed time |
| --- | --- | --- | ---: | ---: |
| `cache unavailable` | MongoDB | `LAB05-T-OK` | 1 | 6190 ms |

The observed 6190 ms is much longer than the configured 500 ms Redis connect and asynchronous operation timeouts. Connection setup, retries, and failure detection contribute to the total elapsed search time.

### Test B: Redis and MongoDB unavailable

MongoDB was stopped while Redis remained stopped. The search printed `cache unavailable` and then a visible `search error` after MongoDB server selection timed out.

| Outcome | Returned result | MongoDB calls | Elapsed time |
| --- | --- | ---: | ---: |
| Search error | None | 1 failed attempt | 8677 ms |

The process set a failure exit code. It did not return `[]`, so an infrastructure failure was not disguised as a valid empty journey result.

### Service restoration

MongoDB and Redis were started again. Redis replied `PONG`, and the MongoDB ping returned `1`. Because Redis persistence is disabled, the next morning search was a cache miss. It successfully returned `LAB05-T-OK` from MongoDB with one MongoDB call in 281 ms.

## Test 7: Concurrent searches

The exact morning key was deleted. Ten searches were then started concurrently with a temporary 200 ms delay immediately before each MongoDB query:

```sh
MONGO_DELAY_MS=200 dotnet run --project database/redis/lecture06/CacheLab -- morning batch
```

| Batch | Cache statuses | Returned trip ID per request | MongoDB calls | Elapsed time |
| --- | --- | --- | ---: | ---: |
| First batch | 10 misses | `LAB05-T-OK` | 10 | 462 ms |
| Second batch before expiry | 10 hits | `LAB05-T-OK` | 0 | 215 ms |

The first batch queried MongoDB ten times because the cache-aside operations are not one atomic operation. All ten requests could read the missing Redis key before any request completed its delayed MongoDB query and populated the cache. They then queried MongoDB concurrently and each wrote an equivalent value to the same key. This overlap is commonly called a cache stampede.

During the second batch, the key already existed before any request started its lookup. All ten requests therefore returned the cached result without querying MongoDB.

### Optional single-flight prevention

Per-key in-process single-flight was then added to `CachedJourneySearch`. The same experiment produced:

| Batch | Cache statuses | Coalesced requests | MongoDB calls | Elapsed time |
| --- | --- | ---: | ---: | ---: |
| First batch after exact-key deletion | 10 misses | 9 | 1 | 465 ms |
| Second batch before expiry | 10 hits | 0 | 0 | 222 ms |

The first request created one shared in-progress operation for the complete cache key. The other nine requests found that operation and awaited it instead of starting their own MongoDB queries. The shared operation fetched `LAB05-T-OK`, wrote Redis once, and returned the same result to all ten callers.

The in-progress entry is removed after success or failure, and removal checks the exact entry so an older request cannot remove a newer operation for the same key. This protection applies within one application process. Multiple application instances would require distributed coordination rather than this in-memory mechanism.

The artificial delay was supplied only through `MONGO_DELAY_MS`. A subsequent normal run printed `MongoDB delay: 0 ms`, confirming that the delay was removed.

## Test 8: Atomic seat counter

Two independent trip counters were stored with 60-second expiries:

| Key | Stored value | Meaning |
| --- | ---: | --- |
| `availability:LAB06:TRIP-ONE` | 0 | Known to have no remaining seats |
| `availability:LAB06:TRIP-TWO` | 10 | Known to have ten remaining seats |

A key containing only route and date could not represent both values. Multiple trips can run on the same route and date while having different capacities and bookings, so availability must be identified at least by trip.

Deleting `availability:LAB06:TRIP-ONE` returned `1`, and the following `GET` returned no value. Stored zero means Redis knows that the trip is sold out. A missing key means availability is unknown, expired, deleted, or never loaded; it must not be interpreted as zero seats.

The atomic script was saved as `database/redis/lecture06/decrement_if_positive.lua`. Starting with one seat produced:

| Operation | Script result | Stored value afterward |
| --- | ---: | ---: |
| First decrement | 0 | 0 |
| Second decrement | -1 | 0 |
| Decrement after deleting key | `availability unknown` | Missing |

Redis executes the Lua script atomically. No other Redis command can change the counter between its `GET`, validation, and `DECR`, so concurrent callers cannot both decrement the same final positive seat.

The script did not reserve a seat in PostgreSQL, create a ticket, or take a payment. It changed only a temporary Redis counter. A complete purchase requires durable authoritative state, payment processing, ticket creation, idempotency, and handling failures across those operations. An atomic Redis decrement protects this one counter operation; it does not make the wider business workflow transactional or prove that a ticket purchase succeeded.

## Test 9: What belongs in Redis

The proposed expiries are not equally suitable:

- Search results at 30 minutes: no. Results include dynamic trip status and availability, so 30 minutes permits stale cancellations and seat counts for too long. I would initially try 2 minutes and adjust from observed traffic and staleness.
- Route/day seat counts at 8 hours: no. Availability changes during booking, and one route/day can contain trips with different counts. I would cache per-trip availability for 30 seconds and invalidate it after reservation or cancellation changes.
- Route information at 24 hours: yes, as an initial value. Routes change slowly, but an administrative update should still delete the key immediately.
- Stop information at 24 hours: yes, for the same reason as route information. Stop corrections and closures should invalidate the key rather than wait for expiry.

| Data | Example key | Example value | Source database | Chosen expiry |
| --- | --- | --- | --- | --- |
| Search results | `search:LAB06:v2:CPH:STOP-NORREPORT:STOP-AIRPORT:2026-10-02T06%3A00%3A00.0000000Z:2026-10-02T07%3A00%3A00.0000000Z` | `{"schemaVersion":2,"cachedAtUtc":"...Z","items":[{"tripId":"LAB05-T-OK"}]}` | MongoDB `journey_search` | 2 minutes |
| Trip availability | `availability:LAB06:TRIP-M2-20260429-0800` | `118` | PostgreSQL `trips` (`capacity - reserved_seats`) | 30 seconds |
| Route information | `route:LAB06:LINE-M2` | `{"id":"LINE-M2","operatorId":"OP-METRO","cityId":"CPH","mode":"metro","shortName":"M2"}` | PostgreSQL `routes` | 24 hours |
| Stop information | `stop:LAB06:STOP-NORREPORT` | `{"id":"STOP-NORREPORT","cityId":"CPH","name":"Nørreport"}` | PostgreSQL `stops` | 24 hours |

### Ownership and invalidation

- Search results are populated by the journey-search service after a MongoDB cache miss. Relevant trip cancellations, schedule changes, fare changes, or projection rebuilds should delete affected keys when practical. Missing or unusable values are fetched from MongoDB.
- Trip availability is updated or invalidated by the reservation workflow after the authoritative PostgreSQL transaction changes `reserved_seats`. It should be deleted after a booking or cancellation if the exact replacement count is not known. Missing or unusable values are recalculated from PostgreSQL, not assumed to be zero.
- Route information is populated by the route/reference-data service. Administrative route changes should update PostgreSQL first and then delete the Redis key. A miss is fetched from PostgreSQL.
- Stop information is populated by the stop/reference-data service. Stop renames, corrections, closures, or city changes should update PostgreSQL first and then delete the Redis key. A miss is fetched from PostgreSQL.

The MongoDB journey document contains a denormalized `availableSeats` field for searching, but PostgreSQL trip capacity and reservations are the authoritative source for a production availability decision.

### Route and stop commands

The route value was stored, read back, and deleted:

```sh
docker compose exec -T redis redis-cli SET 'route:LAB06:LINE-M2' '{"id":"LINE-M2","operatorId":"OP-METRO","cityId":"CPH","mode":"metro","shortName":"M2"}' EX 86400
docker compose exec -T redis redis-cli GET 'route:LAB06:LINE-M2'
docker compose exec -T redis redis-cli DEL 'route:LAB06:LINE-M2'
```

The stop value was stored, read back, and deleted:

```sh
docker compose exec -T redis redis-cli SET 'stop:LAB06:STOP-NORREPORT' '{"id":"STOP-NORREPORT","cityId":"CPH","name":"Nørreport"}' EX 86400
docker compose exec -T redis redis-cli GET 'stop:LAB06:STOP-NORREPORT'
docker compose exec -T redis redis-cli DEL 'stop:LAB06:STOP-NORREPORT'
```

Both `SET` commands returned `OK`, both `GET` commands returned the stored JSON, and both `DEL` commands returned `1`.

### Journey-search sharing

With the chosen 2-minute expiry, two passengers searching one minute apart share an entry only when all five normalized inputs are identical: city, origin, destination, start time, and end time. They then produce the same complete cache key, and the second passenger can read the first passenger's cached result. If their time windows differ, they receive different keys even if the searches occur only a minute apart. Invalidation, expiry, or an unusable value can also prevent sharing.

Even a 2-minute entry can become stale immediately after it is written because of a cancellation, delay, fare change, or seat reservation. The shorter TTL limits how long that staleness can survive; it does not guarantee current data. The purchase path must recheck authoritative availability rather than trust a journey-search cache entry.

## Running the searches

Morning is the default. The optional argument makes the evening test repeatable without editing source code:

```sh
dotnet run --project database/redis/lecture06/CacheLab -- morning
dotnet run --project database/redis/lecture06/CacheLab -- evening
```

Set a temporary expiry for an experiment without changing the 20-second default:

```sh
CACHE_EXPIRY_SECONDS=300 dotnet run --project database/redis/lecture06/CacheLab -- morning
```

Run ten concurrent searches with a temporary MongoDB delay:

```sh
MONGO_DELAY_MS=200 dotnet run --project database/redis/lecture06/CacheLab -- morning batch
```
