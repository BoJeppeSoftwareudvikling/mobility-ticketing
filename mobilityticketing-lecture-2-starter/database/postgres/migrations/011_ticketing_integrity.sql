begin;

alter table trips
    alter column capacity set not null,
    alter column reserved_seats set not null,
    add constraint trips_capacity_non_negative
        check (capacity >= 0),
    add constraint trips_reserved_seats_valid
        check (reserved_seats between 0 and capacity);

alter table products
    alter column price set not null,
    alter column currency set not null,
    add constraint products_price_non_negative
        check (price >= 0),
    add constraint products_currency_iso_code
        check (currency ~ '^[A-Z]{3}$');

alter table tickets
    alter column user_id set not null,
    alter column trip_id set not null,
    alter column ticket_code set not null,
    alter column status set not null,
    alter column product_code set not null,
    alter column valid_from_utc set not null,
    alter column valid_to_utc set not null,
    alter column price set not null,
    alter column currency set not null,
    add constraint tickets_user_fk
        foreign key (user_id) references users(id)
        on delete restrict
        on update restrict,
    add constraint tickets_trip_fk
        foreign key (trip_id) references trips(id)
        on delete restrict
        on update restrict,
    add constraint tickets_product_fk
        foreign key (product_code) references products(code)
        on delete restrict
        on update restrict,
    add constraint tickets_ticket_code_unique
        unique (ticket_code),
    add constraint tickets_id_ticket_code_unique
        unique (id, ticket_code),
    add constraint tickets_status_known
        check (status in ('Active', 'Validated', 'Expired', 'Cancelled')),
    add constraint tickets_validity_window_valid
        check (valid_to_utc >= valid_from_utc),
    add constraint tickets_price_non_negative
        check (price >= 0),
    add constraint tickets_currency_iso_code
        check (currency ~ '^[A-Z]{3}$');

alter table payments
    alter column ticket_id set not null,
    alter column amount set not null,
    alter column currency set not null,
    alter column status set not null,
    alter column created_utc set not null,
    add constraint payments_ticket_fk
        foreign key (ticket_id) references tickets(id)
        on delete restrict
        on update restrict,
    add constraint payments_external_payment_reference_unique
        unique (external_payment_reference),
    add constraint payments_amount_non_negative
        check (amount >= 0),
    add constraint payments_currency_iso_code
        check (currency ~ '^[A-Z]{3}$'),
    add constraint payments_status_known
        check (status in ('Pending', 'Captured', 'Failed', 'Refunded')),
    add constraint payments_captured_reference_required
        check (
            status <> 'Captured'
            or external_payment_reference is not null
        );

alter table validations
    alter column ticket_id set not null,
    alter column ticket_code set not null,
    alter column result set not null,
    alter column validated_utc set not null,
    add constraint validations_ticket_ticket_code_fk
        foreign key (ticket_id, ticket_code)
        references tickets(id, ticket_code)
        on delete restrict
        on update restrict,
    add constraint validations_result_known
        check (result in ('Accepted', 'Rejected'));

commit;
