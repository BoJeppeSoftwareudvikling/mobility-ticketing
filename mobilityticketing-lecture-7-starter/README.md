# MobilityTicketing: Lecture 7 starter

Build a live departure board, saved dispatch plans and booking operations. The [application contract](docs/contract.md) defines the required behavior.

You need Docker Desktop with Compose. Run these commands from this directory:

```sh
docker compose up -d --wait postgres
docker compose build app tests
docker compose run --rm app reset
docker compose run --rm app baseline
docker compose run --rm tests --filter Category=Starter
```

The fixture contains four routes and nine trips. The default window is 23 October 2026, 06:00 to 07:00 UTC. `baseline` uses lazy loading and saves its SQL and results in `docs/evidence/lecture07/logs/`.

Implement the six methods in `StudentApplication.cs`, and put your snapshot schema in `database/postgres/lecture07/03-read-model.sql`. Use your own command and query classes. The [contract](docs/contract.md) defines the required behavior. Keep your design note in `docs/design.md` and your measurements in `docs/evidence/lecture07/README.md`.

After editing code or SQL, rebuild with `docker compose build app tests`. Run `reset` to apply schema changes. It recreates `lab07` and clears bookings and published plans. Tests reset their own database, `mobility_l07_test`.

Once implemented, try this sequence. The initial reset makes it repeatable:

```sh
docker compose run --rm app reset
docker compose run --rm app overview
docker compose run --rm app publish morning
docker compose run --rm app book T201 campus-1
docker compose run --rm app book T201 campus-1
docker compose run --rm app cancel T102
docker compose run --rm app rename R1 "City Ring"
docker compose run --rm app book T102 cancelled-1
docker compose run --rm app overview
docker compose run --rm app snapshot morning
docker compose run --rm app publish revised
docker compose run --rm app snapshot revised
```

The T201 calls return `Booked` and then `AlreadyBooked`, with the same receipt. Booking T102 returns `Cancelled` with exit code 2. The live board shows the cancellation and new route name. The saved `morning` plan keeps its original values; `revised` records the changes.

Compare the reads with more data:

```sh
docker compose run --rm app reset
docker compose run --rm app scale
docker compose run --rm app baseline
docker compose run --rm app overview
```

This fixture has 200 routes and 100,000 trips. Keep the SQL logs and record the executed reads and loaded entities for both sizes.

Run the implementation checks and your own tests:

```sh
docker compose run --rm tests --filter Category=Acceptance
docker compose run --rm tests --filter Category=Student
```

The nine Starter checks pass before you begin. Acceptance checks fail until the application is implemented. `StudentTests.cs` starts empty: add at least four tests and check that they actually run.

For startup problems, check `docker compose logs postgres`. Stop the lab with `docker compose stop`.
