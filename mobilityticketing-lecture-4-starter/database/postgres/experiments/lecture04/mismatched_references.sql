-- This bypasses the new writer and tests what the database itself enforces.
-- The code names DAY while the ID belongs to SINGLE. Both references are valid
-- independently, so the current constraints allow the inconsistent pair.
select id as single_product_id
from products
where code = 'SINGLE'
\gset

begin;

insert into tickets
    (id, user_id, trip_id, ticket_code, status, product_code, product_id,
     valid_from_utc, valid_to_utc, price, currency)
values
    ('LAB04-MISMATCH-1', 'USER-1', 'TRIP-M2-20260429-1200',
     'LAB04-CODE-MISMATCH-1', 'Active', 'DAY',
     :'single_product_id'::uuid,
     '2026-04-29 12:00:00+00', '2026-04-30 12:00:00+00', 65.00, 'DKK');

select t.id, t.product_code, t.product_id, p.code as id_product_code
from tickets t
join products p on p.id = t.product_id
where t.id = 'LAB04-MISMATCH-1';

rollback;
