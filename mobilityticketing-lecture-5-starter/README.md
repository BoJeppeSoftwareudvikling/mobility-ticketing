# MobilityTicketing: Lecture 5 starter

Add MongoDB support for journey search. You need Docker Desktop with Compose. Start both databases from this directory:

```bash
docker compose up -d
docker compose ps
```

PostgreSQL is available at `localhost:5432` (database, user, and password: `mobility`). MongoDB is available at `localhost:27017`. MongoDB setup and query templates are in `database/mongodb/lecture05/`.

Stop the databases:

```bash
docker compose down
```
