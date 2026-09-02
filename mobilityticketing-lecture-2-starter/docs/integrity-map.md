# Integrity map

| Invariant | Classification | Affected tables and columns | Current protection | Missing protection or limitation | Expected failure behaviour | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| Capacity cannot be negative | Direct constraint | `trips.capacity` | `CHECK trips_capacity_non_negative` | Does not solve concurrent overselling | The database rejects the write with a check-constraint error on `trips_capacity_non_negative` | Negative capacity update rejected |
| Reserved seats cannot be negative or greater than capacity | Direct constraint | `trips.reserved_seats`, `trips.capacity` | `CHECK trips_reserved_seats_valid` | not concurrency-safe | The database rejects the write with a check-constraint error on `trips_reserved_seats_valid` | Oversized reservation update rejected |
| Product, ticket, and payment money values cannot be negative | Direct constraint | `products.price`, `tickets.price`, `payments.amount` | `CHECK products_price_non_negative`, `CHECK tickets_price_non_negative`, `CHECK payments_amount_non_negative` | Does not guarantee that ticket price matches product price or that payment amount matches an external capture | The database rejects the write with a check-constraint error on the relevant money constraint | Negative product price, ticket price, or payment amount write rejected |
| Currency must be present and consistently represented | Direct constraint | `products.currency`, `tickets.currency`, `payments.currency` | `NOT NULL` plus `CHECK products_currency_iso_code`, `CHECK tickets_currency_iso_code`, `CHECK payments_currency_iso_code` | Does not guarantee that product, ticket, and payment always use the same currency across the whole purchase flow | The database rejects the write with a not-null or check-constraint error on the relevant currency constraint | Missing currency or lowercase currency code rejected |
| Tickets must reference existing users, trips, and products | Direct constraint | `tickets.user_id`, `tickets.trip_id`, `tickets.product_code` | `FOREIGN KEY tickets_user_fk`, `tickets_trip_fk`, `tickets_product_fk` | Does not decide whether a disabled user may still buy a ticket | The database rejects the write with a foreign-key error on the relevant ticket reference | Ticket insert with unknown user, trip, or product rejected |
| Payments must reference existing tickets | Direct constraint | `payments.ticket_id` | `FOREIGN KEY payments_ticket_fk` | Does not guarantee that one ticket has exactly one payment | The database rejects the write with a foreign-key error on `payments_ticket_fk` | Payment insert with unknown ticket rejected |
| Validations must reference the correct ticket and must not mix one ticket id with another ticket code | Direct constraint | `validations.ticket_id`, `validations.ticket_code`, `tickets.id`, `tickets.ticket_code` | `FOREIGN KEY validations_ticket_ticket_code_fk` supported by `UNIQUE tickets_id_ticket_code_unique` | Does not guarantee that the ticket was still valid at the exact validation time | The database rejects the write with a foreign-key error on `validations_ticket_ticket_code_fk` | Validation insert with mismatched ticket id and ticket code rejected |
| Ticket codes must identify tickets unambiguously | Unique rule | `tickets.ticket_code` | `UNIQUE tickets_ticket_code_unique` | No format or checksum rule | The database rejects the write with a unique-constraint error on `tickets_ticket_code_unique` | Duplicate ticket code insert rejected |
| Ticket validity cannot end before it begins | Direct constraint | `tickets.valid_from_utc`, `tickets.valid_to_utc` | `CHECK tickets_validity_window_valid` | Does not decide whether a ticket may be sold after its validity has already started | The database rejects the write with a check-constraint error on `tickets_validity_window_valid` | Reversed validity window insert rejected |
| Status and result values must come from accepted sets | Direct constraint | `tickets.status`, `payments.status`, `validations.result` | `CHECK tickets_status_known`, `CHECK payments_status_known`, `CHECK validations_result_known` | The exact status vocabulary is a design choice and may need expansion later | The database rejects the write with a check-constraint error on the relevant status or result constraint | Invalid ticket status, payment status, or validation result rejected |
| External payment references must not accidentally represent the same payment more than once | Unique rule | `payments.external_payment_reference` | `UNIQUE payments_external_payment_reference_unique` | Does not prove that the reference actually came from a real gateway capture | The database rejects the write with a unique-constraint error on `payments_external_payment_reference_unique` | Duplicate external payment reference insert rejected |
| Concurrent purchases must not oversell capacity | Cross-row/workflow | `trips.reserved_seats` | None in this migration beyond the row-level seat check | Needs transaction or locking logic | A simple row constraint cannot reliably stop two concurrent purchases from both taking the last seat | Noted for transactions lecture |
| A captured payment must correspond to a real successful gateway capture | External system/workflow | `payments.status`, `payments.external_payment_reference` | Partial local protection from `CHECK payments_captured_reference_required` | Needs external integration and idempotent workflow design | The database can reject obviously incomplete local data, but it cannot prove that the external gateway really captured the payment | Noted for transactions lecture |
| Disabled users must not be able to purchase tickets | Ambiguous domain decision | `users.is_disabled`, `tickets.user_id` | None in this migration | The starter materials do not define whether `is_disabled` means blocked from purchase or only inactive in some other sense | The database does not currently reject such a purchase, because the rule is not clear enough to enforce safely as a simple constraint | Open question noted from the schema and from the lab text mentioning disabled-user purchase as a possible domain or transaction policy |

## Issue register

### Issue 1

- Evidence: `trips_reserved_seats_valid` only checks the final value stored in one `trips` row.
- Problem: Two concurrent purchases can both read available capacity and both increment `reserved_seats`.
- Consequence: Overselling is still possible even though each committed row individually satisfies `0 <= reserved_seats <= capacity`.
- Specific improvement: Handle seat reservation inside a transaction using row locking such as `SELECT ... FOR UPDATE` or an atomic guarded update such as `UPDATE trips SET reserved_seats = reserved_seats + 1 WHERE id = ... AND reserved_seats < capacity`.
- Open question: Will seat allocation be handled by one service or by multiple independent writers?

### Issue 2

- Evidence: `payments_external_payment_reference_unique` prevents duplicate references, and `payments_captured_reference_required` ensures that a captured payment has a reference, but neither proves that the reference corresponds to a real gateway capture.
- Problem: A buggy client or integration could mark a payment as `Captured` without confirmed success at the external payment provider.
- Consequence: Financial data and ticketing data can drift apart even if local column constraints all pass.
- Specific improvement: Introduce an idempotent payment workflow that records gateway callbacks or capture confirmations and only finalizes ticket purchase when the external result is confirmed.
- Open question: Which external identifier is authoritative for idempotency in the real gateway: payment intent id, authorization id, or capture id?

## State-transition trace

### Ticket purchase

1. Preconditions: the referenced `users`, `trips`, and `products` rows already exist, and the chosen `trips` row already satisfies `0 <= reserved_seats <= capacity`.
2. A purchase inserts one `tickets` row. The row must contain an existing `user_id`, `trip_id`, and `product_code`, a unique `ticket_code`, a non-negative `price`, a valid `currency`, and a validity window where `valid_to_utc >= valid_from_utc`.
3. A purchase inserts one `payments` row for that ticket. The row must reference an existing `tickets.id`, store a non-negative `amount`, use a valid `currency`, and use an accepted `status`. If the payment is stored as `Captured`, it must also carry an `external_payment_reference`.
4. A purchase updates `trips.reserved_seats` for the selected trip. The new value must still stay between `0` and `capacity`.
5. After the operation, the database guarantees row-level integrity for the inserted and updated rows, but it does not by itself prevent two concurrent purchases from reserving the same last seat.

### Ticket validation

1. Preconditions: the target ticket already exists and has a stored `(id, ticket_code)` pair.
2. A validation inserts one `validations` row. Its `(ticket_id, ticket_code)` pair must match an existing ticket exactly, so the validation cannot combine the id of one ticket with the code of another.
3. The validation row must contain an accepted `result` value and a non-null `validated_utc` timestamp.
4. An application may also update `tickets.status`, for example from `Active` to `Validated`, but the database only guarantees that the new status belongs to the accepted ticket-status set.
5. The database does not itself prove that the ticket was active, unexpired, or otherwise business-valid at the exact validation moment. That requires workflow logic beyond simple constraints.
