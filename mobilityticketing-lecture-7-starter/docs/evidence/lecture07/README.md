# Route overview measurements

The baseline and live queries returned identical ordered board rows on each fixture for 23 October 2026, 06:00 to 07:00 UTC. Each run below includes the returned rows, executed SQL and bound parameters.

| Routes in fixture | Run | SQL reads | Route entities loaded | Trip entities loaded |
| ---: | --- | ---: | ---: | ---: |
| 4 | [baseline](4-routes-baseline.json) | 5 | 4 | 9 |
| 4 | [live](4-routes-live.json) | 1 | 0 | 0 |
| 200 | [baseline](200-routes-baseline.json) | 201 | 200 | 100000 |
| 200 | [live](200-routes-live.json) | 1 | 0 | 0 |

To reproduce, follow the [lecture setup](../../../README.md), then run the app commands in this order: `reset`, `baseline`, `overview`, `scale`, `baseline`, `overview`. The small fixture contains nine trips and the scaled fixture contains 100,000. The live query still returns one row per route when there are zero scheduled departures in the window.
