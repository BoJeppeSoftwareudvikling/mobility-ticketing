-- Run each statement separately after applying your integrity migration.
-- Every statement below should be rejected by a named constraint.

-- 1. Negative capacity.
-- Expected: rejected with a check-constraint error mentioning `trips_capacity_non_negative`.
update trips
set capacity = -1
where id = 'TRIP-M2-20260429-0800';

-- 2. More reserved seats than capacity.
-- Expected: rejected with a check-constraint error mentioning `trips_reserved_seats_valid`.
update trips
set reserved_seats = capacity + 1
where id = 'TRIP-M2-20260429-0800';

-- 3. Negative product price.
-- Expected: rejected with a check-constraint error mentioning `products_price_non_negative`.
update products
set price = -1
where code = 'SINGLE';

-- 4. Invalid product currency format.
-- Expected: rejected with a check-constraint error mentioning `products_currency_iso_code`.
update products
set currency = 'dkk'
where code = 'SINGLE';

-- 5. Ticket with unknown user.
-- Expected: rejected with a foreign-key error mentioning `tickets_user_fk`.
insert into tickets (
    id, user_id, trip_id, ticket_code, status,
    product_code, valid_from_utc, valid_to_utc, price, currency
) values (
    'T-UNKNOWN-USER', 'USER-DOES-NOT-EXIST', 'TRIP-M2-20260429-0800',
    'CODE-UNKNOWN-USER', 'Active', 'SINGLE',
    '2026-04-29 08:00:00+00', '2026-04-29 09:00:00+00', 36.00, 'DKK'
);

-- 6. Ticket with unknown trip.
-- Expected: rejected with a foreign-key error mentioning `tickets_trip_fk`.
insert into tickets (
    id, user_id, trip_id, ticket_code, status,
    product_code, valid_from_utc, valid_to_utc, price, currency
) values (
    'T-UNKNOWN-TRIP', 'USER-1', 'TRIP-DOES-NOT-EXIST',
    'CODE-UNKNOWN-TRIP', 'Active', 'SINGLE',
    '2026-04-29 08:00:00+00', '2026-04-29 09:00:00+00', 36.00, 'DKK'
);

-- 7. Ticket with unknown product.
-- Expected: rejected with a foreign-key error mentioning `tickets_product_fk`.
insert into tickets (
    id, user_id, trip_id, ticket_code, status,
    product_code, valid_from_utc, valid_to_utc, price, currency
) values (
    'T-UNKNOWN-PRODUCT', 'USER-1', 'TRIP-M2-20260429-0800',
    'CODE-UNKNOWN-PRODUCT', 'Active', 'NO-SUCH-PRODUCT',
    '2026-04-29 08:00:00+00', '2026-04-29 09:00:00+00', 36.00, 'DKK'
);

-- 8. Missing ticket currency.
-- Expected: rejected with a not-null error on `tickets.currency`.
insert into tickets (
    id, user_id, trip_id, ticket_code, status,
    product_code, valid_from_utc, valid_to_utc, price, currency
) values (
    'T-NULL-CURRENCY', 'USER-1', 'TRIP-M2-20260429-0800',
    'CODE-NULL-CURRENCY', 'Active', 'SINGLE',
    '2026-04-29 08:00:00+00', '2026-04-29 09:00:00+00', 36.00, null
);

-- 9. Reversed ticket validity window.
-- Expected: rejected with a check-constraint error mentioning `tickets_validity_window_valid`.
insert into tickets (
    id, user_id, trip_id, ticket_code, status,
    product_code, valid_from_utc, valid_to_utc, price, currency
) values (
    'T-REVERSED', 'USER-1', 'TRIP-M2-20260429-0800',
    'CODE-REVERSED', 'Active', 'SINGLE',
    '2026-04-29 09:00:00+00', '2026-04-29 08:00:00+00', 36.00, 'DKK'
);

-- 10. Duplicate ticket code.
-- Expected: rejected with a unique-constraint error mentioning `tickets_ticket_code_unique`.
insert into tickets (
    id, user_id, trip_id, ticket_code, status,
    product_code, valid_from_utc, valid_to_utc, price, currency
)
select
    'T-DUPLICATE-CODE', user_id, trip_id, ticket_code, status,
    product_code, valid_from_utc, valid_to_utc, price, currency
from tickets
where id = 'TICKET-1';

-- 11. Unknown ticket status.
-- Expected: rejected with a check-constraint error mentioning `tickets_status_known`.
update tickets
set status = 'Unknown'
where id = 'TICKET-1';

-- 12. Negative ticket price.
-- Expected: rejected with a check-constraint error mentioning `tickets_price_non_negative`.
update tickets
set price = -1
where id = 'TICKET-1';

-- 13. Payment for an unknown ticket.
-- Expected: rejected with a foreign-key error mentioning `payments_ticket_fk`.
insert into payments (
    id, user_id, ticket_id, external_payment_reference,
    amount, currency, status
) values (
    'PAYMENT-UNKNOWN-TICKET', 'USER-1', 'NO-SUCH-TICKET',
    'gateway-capture-invalid', 36.00, 'DKK', 'Captured'
);

-- 14. Negative payment amount.
-- Expected: rejected with a check-constraint error mentioning `payments_amount_non_negative`.
update payments
set amount = -1
where id = 'PAYMENT-1';

-- 15. Invalid payment currency format.
-- Expected: rejected with a check-constraint error mentioning `payments_currency_iso_code`.
update payments
set currency = 'dkk'
where id = 'PAYMENT-1';

-- 16. Unknown payment status.
-- Expected: rejected with a check-constraint error mentioning `payments_status_known`.
update payments
set status = 'Unknown'
where id = 'PAYMENT-1';

-- 17. Duplicate external payment reference.
-- Expected: rejected with a unique-constraint error mentioning `payments_external_payment_reference_unique`.
insert into payments (
    id, user_id, ticket_id, external_payment_reference,
    amount, currency, status
) values (
    'PAYMENT-DUPLICATE-REFERENCE', 'USER-1', 'TICKET-1',
    'gateway-capture-0001', 36.00, 'DKK', 'Captured'
);

-- 18. Captured payment without external reference.
-- Expected: rejected with a check-constraint error mentioning `payments_captured_reference_required`.
insert into payments (
    id, user_id, ticket_id, external_payment_reference,
    amount, currency, status
) values (
    'PAYMENT-NO-REF', 'USER-1', 'TICKET-1',
    null, 36.00, 'DKK', 'Captured'
);

-- 19. Validation with mismatched ticket id and ticket code.
-- Expected: rejected with a foreign-key error mentioning `validations_ticket_ticket_code_fk`.
insert into validations (
    id, ticket_id, ticket_code, vehicle_id, stop_id, device_id, result
) values (
    'VALIDATION-MISMATCH', 'TICKET-1', 'CODE-5C-0001',
    'BUS-5C-01', 'STOP-CENTRAL', 'DEVICE-01', 'Accepted'
);

-- 20. Unknown validation result.
-- Expected: rejected with a check-constraint error mentioning `validations_result_known`.
update validations
set result = 'Maybe'
where id = 'VALIDATION-1';
