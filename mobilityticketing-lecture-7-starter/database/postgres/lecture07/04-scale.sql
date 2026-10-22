insert into lab07.routes(id,short_name)
select 'S' || lpad((n+4)::text,3,'0'), 'Scale route ' || (n+4)
from generate_series(1,196) n on conflict do nothing;

insert into lab07.trips(id,route_id,scheduled_departure_utc,status)
select 'LOAD' || lpad(n::text,6,'0'),
       'S' || lpad((5 + (n-1)%196)::text,3,'0'),
       '2026-10-23T06:00:00Z'::timestamptz + (n%120)*interval '1 minute',
       case when n%13=0 then 'Cancelled' else 'Scheduled' end
from generate_series(1,99991) n on conflict do nothing;
