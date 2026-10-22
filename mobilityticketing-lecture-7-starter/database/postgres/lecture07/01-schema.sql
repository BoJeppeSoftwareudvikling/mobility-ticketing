create schema if not exists lab07;
create table lab07.routes (
    id text primary key,
    short_name text not null check (btrim(short_name) <> '')
);
create table lab07.trips (
    id text primary key,
    route_id text not null references lab07.routes(id),
    scheduled_departure_utc timestamptz not null,
    status text not null check (status in ('Scheduled', 'Cancelled'))
);
create table lab07.bookings (
    request_id text primary key,
    trip_id text not null references lab07.trips(id),
    booked_at_utc timestamptz not null
);
create index trips_route_window on lab07.trips(route_id, scheduled_departure_utc) include (status);
