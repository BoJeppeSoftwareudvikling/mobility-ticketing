#!/bin/sh
set -eu
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres <<'SQL'
SELECT 'CREATE DATABASE mobility_l07_test' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'mobility_l07_test')
\gexec
SQL
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname mobility_l07_test -f /docker-entrypoint-initdb.d/01-schema.sql
