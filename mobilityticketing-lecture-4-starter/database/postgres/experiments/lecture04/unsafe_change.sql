-- Use only your disposable lab database. This deliberately demonstrates why
-- replacing a populated reference in one step is unsafe.
begin;

alter table tickets
    drop column product_code;

alter table tickets
    add column product_id uuid;

-- Existing tickets receive NULL for the new column, so this statement fails.
alter table tickets
    alter column product_id set not null;

rollback;
-- With ON_ERROR_STOP disabled, psql reaches this rollback after the expected
-- error. With ON_ERROR_STOP enabled, closing the connection rolls it back.
