-- Test setup: obtain the product ID that a new caller would already have.
select id as selected_product_id
from products
where code = 'DAY'
\gset

-- product_code is deliberately not a caller input. It is derived from the
-- product selected by product_id. Price and currency remain ticket inputs.
with input (
    ticket_id, user_id, trip_id, ticket_code, status, product_id,
    valid_from_utc, valid_to_utc, agreed_price, agreed_currency
) as (
    values (
        'LAB04-NEW-1', 'USER-2', 'TRIP-5C-20260429-1700',
        'LAB04-CODE-NEW-1', 'Active', :'selected_product_id'::uuid,
        '2026-04-29 16:45:00+00'::timestamptz,
        '2026-04-30 16:45:00+00'::timestamptz,
        65.00::numeric, 'DKK'
    )
)
insert into tickets
    (id, user_id, trip_id, ticket_code, status, product_code, product_id,
     valid_from_utc, valid_to_utc, price, currency)
select i.ticket_id, i.user_id, i.trip_id, i.ticket_code, i.status,
       p.code, p.id, i.valid_from_utc, i.valid_to_utc,
       i.agreed_price, i.agreed_currency
from input i
join products p on p.id = i.product_id;
