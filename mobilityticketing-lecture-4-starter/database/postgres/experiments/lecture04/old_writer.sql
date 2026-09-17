-- The old writer knows only the product code. product_id remains null.
insert into tickets
    (id, user_id, trip_id, ticket_code, status, product_code,
     valid_from_utc, valid_to_utc, price, currency)
values
    ('LAB04-OLD-1', 'USER-1', 'TRIP-M2-20260429-1200',
     'LAB04-CODE-OLD-1', 'Active', 'SINGLE',
     '2026-04-29 12:00:00+00', '2026-04-29 14:00:00+00', 36.00, 'DKK');
