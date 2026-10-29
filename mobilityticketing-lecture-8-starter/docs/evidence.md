# Query optimization notes

Use the `Execution Time` from each of the five measured plans. Sort the five values to find the median. Record the smallest and largest as the range.

| Route | Query / active index | Median ms | Range ms | Scan / filter / sort observation | Top-node shared hit / read buffers | Check passes? |
| --- | --- | --- | --- | --- | --- | --- |
| R-7 | Baseline / primary key only | | | | | |
| R-7 | Rewrite / primary key only | | | | | |
| R-7 | Rewrite / route-only index | | | | | |
| R-7 | Rewrite / chosen index | | | | | |
| R-0 | Baseline / primary key only | | | | | |
| R-0 | Rewrite / primary key only | | | | | |
| R-0 | Rewrite / route-only index | | | | | |
| R-0 | Rewrite / chosen index | | | | | |

## Keep or remove?

- Index sizes: route-only ___; chosen index ___
- Index to keep:
- One result that supports the decision:
- One alternative rejected, and why:
- What changes when a scheduled departure is inserted, rescheduled or cancelled?
- Your AI prompt:
- One AI suggestion accepted, changed or rejected, and the check that decided it:

## Partitioning

| Read | Plain-table plan | Partitions actually accessed | Same result? |
| --- | --- | --- | --- |
| October count | | | |
| Ticket 100 history | | | |

Would you choose monthly partitions for reporting, ticket support, or monthly retention? Refer to the plans.
