create table lab07.published_plans (
    id text primary key check (btrim(id) <> ''),
    from_utc timestamptz not null,
    to_utc timestamptz not null,
    published_at_utc timestamptz not null,
    check (to_utc > from_utc and to_utc - from_utc <= interval '24 hours')
);

create table lab07.published_plan_routes (
    plan_id text not null references lab07.published_plans(id) on delete cascade,
    route_id text not null,
    route_name text not null check (btrim(route_name) <> ''),
    scheduled_trip_count integer not null check (scheduled_trip_count >= 0),
    next_departure_utc timestamptz,
    primary key (plan_id, route_id),
    check (
        (scheduled_trip_count = 0 and next_departure_utc is null)
        or (scheduled_trip_count > 0 and next_departure_utc is not null)
    )
);
