begin;

insert into products (code, name, price, currency) values
    ('SINGLE-TEST', 'Single trip test', 36.00, 'DKK');

update trips
set reserved_seats = reserved_seats + 1
where id = 'TRIP-M2-20260429-1200';

insert into tickets (
    id, user_id, trip_id, ticket_code, status,
    product_code, valid_from_utc, valid_to_utc, price, currency
) values (
    'TICKET-OK-1', 'USER-1', 'TRIP-M2-20260429-1200',
    'CODE-M2-0099', 'Active', 'SINGLE-TEST',
    '2026-04-29 11:45:00+00', '2026-04-29 14:00:00+00',
    36.00, 'DKK'
);

insert into payments (
    id, user_id, ticket_id, external_payment_reference,
    amount, currency, status, created_utc
) values (
    'PAYMENT-OK-1', 'USER-1', 'TICKET-OK-1',
    'gateway-capture-0099', 36.00, 'DKK', 'Captured',
    '2026-04-29 11:40:00+00'
);

insert into validations (
    id, ticket_id, ticket_code, vehicle_id,
    stop_id, device_id, result, validated_utc
) values (
    'VALIDATION-OK-1', 'TICKET-OK-1', 'CODE-M2-0099',
    'METRO-M2-01', 'STOP-KONGENS-NYTORV', 'DEVICE-99',
    'Accepted', '2026-04-29 12:05:00+00'
);

update tickets
set status = 'Validated'
where id = 'TICKET-OK-1';

select id, capacity, reserved_seats
from trips
where id = 'TRIP-M2-20260429-1200';

select code, price, currency
from products
where code = 'SINGLE-TEST';

select id, ticket_code, status, price, currency
from tickets
where id = 'TICKET-OK-1';

select id, ticket_id, external_payment_reference, status
from payments
where id = 'PAYMENT-OK-1';

select id, ticket_id, ticket_code, result
from validations
where id = 'VALIDATION-OK-1';

rollback;
