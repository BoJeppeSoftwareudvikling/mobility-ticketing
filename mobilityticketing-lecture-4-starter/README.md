# MobilityTicketing: Lecture 4 starter

Give products stable IDs without breaking existing tickets or the application code that still uses product codes.

You need Docker Desktop with Compose. Start the database from this directory:

```bash
docker compose up -d
docker compose ps
```

Connect at `localhost:5432` with database, user and password `mobility`. Stop other lecture containers first if they use the same port.

Read [the lab](docs/lab.md) for the tasks and commands. You will find:

- the starting schema and data in `database/postgres/init/`;
- migration examples to complete in `database/postgres/migrations/`;
- queries, inserts and experiments in `database/postgres/experiments/lecture04/`.

The database contains three tickets covering two products. One DAY ticket was bought for 65 DKK; the catalogue now lists 80 DKK. Your migration must keep the price paid.

Leave the initialization files unchanged and put your changes in migrations. If you use your own repository, keep your earlier constraints and check for reporting views or functions that depend on the columns you change.

To stop the database:

```bash
docker compose down
```

To discard your lab data and load the starting data again:

```bash
docker compose down -v
docker compose up -d
```
