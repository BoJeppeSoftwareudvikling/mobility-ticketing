# MobilityTicketing: Lecture 8 starter

Investigate departure searches and monthly validation history. The [lecture page](https://app.notion.com/p/Query-Optimization-3b302628a6028055b11ff193efea11bf) contains the tasks. This README provides setup and commands.

You need Docker with Compose. Extract the starter and run commands from this directory.

## Start

```sh
docker compose up -d --wait postgres
docker compose exec -T postgres bash /lab/run.sh reset
docker compose exec -T postgres bash /lab/run.sh run baseline R-7
docker compose exec -T postgres bash /lab/run.sh check
```

The database is `mobility_l08`, with user and password `mobility`. Commands run inside the container, so no host port or local PostgreSQL installation is needed. This fixture is independent of your Lecture 7 implementation.

`reset` recreates **only `lab08`**, including its data and experimental indexes. It leaves your edited SQL files in place. The fixture contains 120,008 trips across September, October and November 2026. On 23 October, R-7 has 38 scheduled departures and R-0 has 991. The search returns the first 20, ordered by departure time and ID.

There are 120,000 validation events in each of two equivalent copies. October contains 40,889 events. Ticket 100 has 12 events across the three months. Both validation tables use the key `(validated_utc, id)`, which enforces the uniqueness of that pair, not of `id` alone.

## Your files

| File | Use |
| --- | --- |
| `queries/candidate.sql` | Rewrite the departure query. It starts as the baseline. |
| `queries/indexes.sql` | Create and drop your experimental indexes, one candidate at a time. |
| `docs/evidence.md` | Record the comparison and your recommendation. |

Keep `queries/baseline.sql`, `database/setup.sql` and `run.sh` unchanged. The helper supplies `route_id`, `query_day` and `result_limit` to both queries. Use those variables in your rewrite. The day is 23 October 2026 **in UTC**, regardless of your computer's timezone.

## Run and measure

Before adding any index, save the baseline for both routes:

```sh
docker compose exec -T postgres bash /lab/run.sh measure baseline R-7 > docs/baseline-r7.txt
docker compose exec -T postgres bash /lab/run.sh measure baseline R-0 > docs/baseline-r0.txt
```

After editing `candidate.sql`, run:

```sh
docker compose exec -T postgres bash /lab/run.sh check
docker compose exec -T postgres bash /lab/run.sh measure candidate R-7 > docs/rewrite-r7.txt
docker compose exec -T postgres bash /lab/run.sh measure candidate R-0 > docs/rewrite-r0.txt
```

`measure` runs one unrecorded warm-up followed by five `EXPLAIN (ANALYZE, BUFFERS)` runs. Use the `Execution Time` line in each plan, not the wall time of Docker starting a command. Buffer counts at the top node include work below it, so do not add every node's counts together. `shared hit` means found in PostgreSQL's buffer cache. `shared read` does not establish that a physical disk access occurred, because the operating system may cache the data too.

After writing your index statements, apply them and measure again:

```sh
docker compose exec -T postgres bash /lab/run.sh indexes
docker compose exec -T postgres bash /lab/run.sh check
docker compose exec -T postgres bash /lab/run.sh measure candidate R-7 > docs/index-r7.txt
docker compose exec -T postgres bash /lab/run.sh measure candidate R-0 > docs/index-r0.txt
```

Use distinct output filenames for each index candidate. `indexes` also lists the installed trip indexes and their sizes. Remove your previous experimental index before testing the next one. Keep the primary-key index.

## Check the result

`check` compares the complete returned rows in order, for R-7 and R-0. It checks both the first 20 and the full day, so an incorrect upper boundary cannot hide beyond the first page.

For R-7, the full day includes trip 900001 at midnight and 900003 at the final microsecond. It excludes 900002 at the following midnight and cancelled trip 900004. R-0 has the corresponding cases 900011–900014. All four checks should pass before you start, since `candidate.sql` initially matches the baseline.

## Inspect partitioning

```sh
docker compose exec -T postgres bash /lab/run.sh partitions > docs/partition-plans.txt
```

The supplied script compares an October count and a ticket-history lookup on the plain and partitioned copies. Both ticket counts should be 12 and `different_ticket_history_rows` should be 0. It creates no new indexes and changes no data. Read the table names in the plans to see which partitions were accessed.

For startup problems, use `docker compose logs postgres`. Stop with `docker compose stop`.
