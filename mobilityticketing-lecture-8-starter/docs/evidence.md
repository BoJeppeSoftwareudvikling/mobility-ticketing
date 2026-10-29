# Query optimization notes

Use the `Execution Time` from each of the five measured plans. Sort the five values to find the median. Record the smallest and largest as the range.

| Route | Query / active index | Median ms | Range ms | Scan / filter / sort observation | Top-node shared hit / read buffers | Check passes? |
| --- | --- | --- | --- | --- | --- | --- |
| R-7 | Baseline / primary key only | 6.610 | 6.517–6.729 | `Seq Scan` reads all 120,008 rows, returns 38 and removes 119,970 by filter. `quicksort` sorts the 38 matching rows before `Limit` returns 20. | 1007 / 0 | Yes: 20-row page and 38-row full day |
| R-7 | Rewrite / primary key only | 4.827 | 4.759–5.145 | Plan shape is unchanged: `Seq Scan -> Sort -> Limit`. The range predicate improves the row estimate from 15 to 32; actual rows remain 38. | 1007 / 0 | Yes: 20-row page and 38-row full day |
| R-7 | Rewrite / route-only index | 1.828 | 1.816–2.002 | `Bitmap Index Scan` finds 3,337 route rows. `Bitmap Heap Scan` visits all 1,001 heap blocks, removes 3,299 rows by date/status, and still sorts. | 1011 / 0 | Yes: 20-row page and 38-row full day |
| R-7 | Rewrite / chosen partial multicolumn index | 0.131 | 0.128–0.132 | Ordered `Index Scan` uses route and UTC range as `Index Cond`. No filter or separate sort appears; the scan stops after 20 rows. | 23 / 0 | Yes: 20-row page and 38-row full day |
| R-0 | Baseline / primary key only | 11.906 | 11.203–12.027 | `Seq Scan` reads all 120,008 rows, returns 991 and removes 119,017 by filter. `top-N heapsort` finds the first 20 in departure order. | 1007 / 0 | Yes: 20-row page and 991-row full day |
| R-0 | Rewrite / primary key only | 5.056 | 4.850–5.479 | Plan shape is unchanged: `Seq Scan -> Sort -> Limit`. The range predicate improves the row estimate from 414 to 904; actual rows remain 991. | 1007 / 0 | Yes: 20-row page and 991-row full day |
| R-0 | Rewrite / route-only index | 5.134 | 4.832–5.448 | PostgreSQL rejects the low-selectivity route index for busy R-0 and keeps `Seq Scan -> top-N heapsort -> Limit`. | 1007 / 0 | Yes: 20-row page and 991-row full day |
| R-0 | Rewrite / chosen partial multicolumn index | 0.134 | 0.130–0.136 | Ordered `Index Scan` uses route and UTC range as `Index Cond`. No filter or separate sort appears; the scan stops after 20 rows. | 24 / 0 | Yes: 20-row page and 991-row full day |

## Baseline observation and prediction

The main work to reduce is the full sequential scan and its filtering. Both routes touch 1,001 table buffers and inspect all 120,008 rows even though only 38 rows match R-7 and 991 match R-0.

Prediction: rewriting the date predicate as a half-open UTC range will make the departure-time condition usable as an index condition. A later index that starts with the route filter and continues with departure time and ID should let PostgreSQL read a much smaller ordered part of the data. This should reduce rows removed by filter and buffer accesses, and it may remove the explicit sort because the index can supply the requested order. The rewrite alone may leave the sequential scan unchanged while no suitable index exists.

### Why the two baseline plans differ

Both plans have the same shape: `Seq Scan -> Sort -> Limit`. `LIMIT 20` cannot stop the sequential scan early because PostgreSQL must first find the matching rows and determine which 20 have the earliest `(scheduled_departure_utc, id)` values.

R-7 produces only 38 rows before the limit. The planner estimated 15 rows, fewer than the limit, and the sort used `quicksort` on all matching rows. R-0 produces 991 rows. The planner estimated 414 rows, so the sort used `top-N heapsort` to retain only the best 20 rows instead of fully sorting every match.

R-0 has the higher median even though both plans touch the same number of buffers. Its scan/filter node passes far more rows to the sort, so more candidate rows must be handled before the first 20 can be returned. The plan proves this difference in row processing, but it does not isolate the exact CPU cost of each predicate.

## What changed after the UTC range rewrite?

The original predicate applies a function and cast to every candidate timestamp:

```sql
(scheduled_departure_utc at time zone 'UTC')::date = :'query_day'::date
```

The candidate query instead compares the column directly with an inclusive UTC start and exclusive UTC end:

```sql
scheduled_departure_utc >= (:'query_day'::date::timestamp at time zone 'UTC')
and scheduled_departure_utc < ((:'query_day'::date + 1)::timestamp at time zone 'UTC')
```

`check` proves that midnight at the start is included and the following midnight is excluded for both the 20-row page and the full day. With only the primary key, PostgreSQL still uses a sequential scan and still touches 1,007 top-node buffers. The rewrite therefore does not reduce I/O by itself. It does make the predicate usable as a B-tree range condition, improves the estimates, and removes the per-row timestamp-to-date expression. The lower measured times are consistent with less CPU work, but timing alone is not the reason for keeping the rewrite; index compatibility and correct boundaries are.

## Index comparison

The 832 kB route-only index is selective enough for R-7, so PostgreSQL uses a bitmap scan. The matching route rows are spread across all 1,001 heap blocks, however, and date/status filtering plus sorting remain. For R-0, the route is so common that PostgreSQL correctly prefers a sequential scan. The index therefore does not support both route distributions well.

The chosen 4,408 kB index is:

```sql
create index trips_scheduled_route_departure_id_idx
on lab08.trips (route_id, scheduled_departure_utc, id)
where status = 'Scheduled';
```

`route_id` is first because the query has an equality predicate on the route. `scheduled_departure_utc` follows so PostgreSQL can use the UTC range and read rows in departure order. `id` completes the requested ordering when departure times tie. The partial predicate keeps cancelled trips out and is usable because the query explicitly requires `status = 'Scheduled'`.

Both routes change to `Limit -> Index Scan`. Filtering and the separate sort disappear, and the scan can stop when it has found 20 ordered rows. Top-node buffers fall from about 1,007 to 23–24.

## Keep or remove?

- Index sizes: route-only `832 kB`; chosen partial multicolumn index `4,408 kB`.
- Index to keep: `trips_scheduled_route_departure_id_idx` on `(route_id, scheduled_departure_utc, id) where status = 'Scheduled'`.
- One result that supports the decision: both routes use an ordered index scan with no filter or sort, while top-node buffers fall to 23–24 and medians fall to 0.131–0.134 ms.
- One alternative rejected: `trips_route_id_idx`. It leaves filtering and sorting for R-7 and is not used at all for R-0.
- Write cost: inserting a scheduled trip adds an index entry; rescheduling changes its ordered timestamp entry; cancelling it removes the entry; changing a cancelled trip back to scheduled adds it again. These writes maintain a 4,408 kB index in addition to the primary key. Cancelled trips do not occupy this partial index.

### AI second-opinion prompt

```text
PostgreSQL 17 must return the first 20 Scheduled trips for one route and one
UTC day, ordered by scheduled_departure_utc and then id.

Query:
select id, route_id, scheduled_departure_utc, status
from lab08.trips
where route_id = :route_id
  and status = 'Scheduled'
  and scheduled_departure_utc >= :utc_day_start
  and scheduled_departure_utc < :next_utc_day_start
order by scheduled_departure_utc, id
limit :result_limit;

Table:
trips(id bigint primary key, route_id text not null,
      scheduled_departure_utc timestamptz not null,
      status text not null)

Existing index: trips_pkey on (id). The baseline plan is
Limit -> Sort -> Seq Scan. It scans 120,008 rows, removes 119,970 for R-7
or 119,017 for R-0, and touches 1,007 top-node buffers.

Suggest exactly one candidate B-tree index. Explain the column order, whether
it should be partial for Scheduled trips, how it can support ORDER BY, and its
write costs. Do not remove the primary key.
```

The AI suggestion was the partial index on `(route_id, scheduled_departure_utc, id) where status = 'Scheduled'`. It was accepted only after `check` passed for both the first page and full day, and measured plans for both R-7 and R-0 removed the filter and sort and reduced buffers to 23–24.

## Partitioning

| Read | Plain-table plan | Partitions actually accessed | Same result? |
| --- | --- | --- | --- |
| October count | `Index Only Scan` on `validations_plain_pkey`; 40,889 rows, 198 shared hits, 3.935 ms | Only `validations_oct`; September and November are pruned. The remaining October partition uses a `Seq Scan`, 261 shared hits, 3.723 ms. | Yes: both return 40,889 |
| Ticket 100 history | `Seq Scan` on all 120,000 plain-table rows, then sort; 12 rows, 771 top-node shared hits, 3.485 ms | `validations_sep`, `validations_oct`, and `validations_nov` are all scanned under `Append`, then sorted; 12 rows, 766 shared hits, 3.436 ms. | Yes: both return 12 and the difference count is 0 |

### Partitioning decision

Monthly partitioning helps PostgreSQL rule out entire months only when the predicate constrains the partition key, `validated_utc`. That happens for the October report, where only `validations_oct` appears. In this fixture the plain table already has a primary-key index beginning with `validated_utc`, so its index-only scan is about as fast and touches fewer buffers than the partitioned sequential scan. The measured reads alone therefore do not justify partitioning.

Ticket history has only `ticket_id = 100` and no known date range. PostgreSQL cannot prune by `validated_utc`, so it scans September, October, and November. Monthly partitioning does not improve this access pattern; a suitable ticket lookup index would address it more directly.

I would choose monthly partitions if month-based retention is an important operational requirement. An old month can be detached or dropped as one partition instead of deleting many rows individually, which also avoids maintaining indexes row by row for that removal. The choice would then be justified by lifecycle management and month-bounded workloads, not by the ticket-history read.
