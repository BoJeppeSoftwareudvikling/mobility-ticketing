-- Test setup: obtain the product ID that the caller supplies.
select id as selected_product_id
from products
where code = 'SINGLE'
\gset

-- The final writer stores only product_id. The product code remains on the
-- product, while the agreed price and currency remain historical ticket data.
insert into tickets
    (id, user_id, trip_id, ticket_code, status, product_id,
     valid_from_utc, valid_to_utc, price, currency)
values
    ('LAB04-FINAL-1', 'USER-1', 'TRIP-M2-20260429-1200',
     'LAB04-CODE-FINAL-1', 'Active', :'selected_product_id'::uuid,
     '2026-04-29 12:00:00+00', '2026-04-29 14:00:00+00', 36.00, 'DKK');
