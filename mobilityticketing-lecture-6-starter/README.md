# MobilityTicketing: Lecture 6 starter

Add Redis caching around your journey search. Continue in your existing repository, or use this starter with the lecture 5 fixture.

You need Docker Desktop with Compose and .NET 8 or later for the optional C# helper. Start MongoDB and Redis from this directory:

```sh
docker compose up -d mongo redis
docker compose exec -T redis redis-cli PING
docker compose exec -T mongo mongosh --quiet --file /scripts/lecture05/reset.js
```

Run the uncached C# helper:

```sh
dotnet run --project database/redis/lecture06/CacheLab
```

Expect only `LAB05-T-OK` and one MongoDB call. MongoDB is available at `localhost:27017`; Redis at `localhost:6379`. PostgreSQL setup is retained from the lecture 5 starter.

The helper supplies the original `journey_search` query. Adapt it if you use the alternative collection. Implement your key function, cache-aside wrapper and failure handling in `database/redis/lecture06/`. Save the Lua script from the lab there. Record your observations in `docs/evidence/lecture06/README.md` and link them from your existing `docs/dossier.md`.

Follow the Implementation lab in Moodle for the exercises and expected results. Redis persistence is disabled because it is a disposable cache for this lab.

Stop the services:

```sh
docker compose down
```
