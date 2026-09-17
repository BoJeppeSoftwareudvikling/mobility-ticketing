# Lecture 4 evidence

## Unsafe one-step migration

The deliberately unsafe migration is saved in
`database/postgres/experiments/lecture04/unsafe_change.sql`. Run it only on the
disposable lab database:

```bash
docker compose exec -T postgres psql -U mobility -d mobility \
  < database/postgres/experiments/lecture04/unsafe_change.sql
```

The first two `ALTER TABLE` statements remove `tickets.product_code` and add a
nullable `product_id`. The attempt to require the new column then fails because
all existing tickets received a null value:

```text
ERROR:  column "product_id" of relation "tickets" contains null values
```

The script wraps the experiment in a transaction and rolls it back after the
expected error. Reset the disposable database before continuing so the proper
migration starts from known seed data:

```bash
docker compose down -v
docker compose up -d
docker compose exec -T postgres psql -U mobility -d mobility \
  -v ON_ERROR_STOP=1 \
  < database/postgres/experiments/lecture04/baseline.sql
```

This migration is unsafe because `product_code` is the only existing link from
a ticket to its product. Dropping it before using it to backfill `product_id`
removes the information needed to reconstruct that link. Adding `product_id`
to a populated table gives every existing ticket a null value, so it cannot be
made required until those values have been backfilled and checked. Existing
readers and writers also still select or insert `product_code`; removing the
column immediately breaks them. The safe migration must therefore add the new
reference without removing the old one, backfill and verify it, migrate the
application code, and only then require the new reference and retire the old
column.

## Mixed-version readers and writers

After applying `030_expand_product_identity.sql`, run the old and new writers,
then both readers:

```bash
docker compose exec -T postgres psql -U mobility -d mobility \
  -v ON_ERROR_STOP=1 \
  < database/postgres/experiments/lecture04/old_writer.sql
docker compose exec -T postgres psql -U mobility -d mobility \
  -v ON_ERROR_STOP=1 \
  < database/postgres/experiments/lecture04/new_writer.sql
docker compose exec -T postgres psql -U mobility -d mobility \
  -v ON_ERROR_STOP=1 \
  < database/postgres/experiments/lecture04/old_reader.sql
docker compose exec -T postgres psql -U mobility -d mobility \
  -v ON_ERROR_STOP=1 \
  < database/postgres/experiments/lecture04/new_reader.sql
```

`old_writer.sql` leaves `product_id` null, so it continues to satisfy the old
contract while the new reference is optional. `new_writer.sql` accepts only a
product ID, joins to that product, and stores the product's code. A caller
therefore cannot supply a conflicting code through this writer. The agreed
price and currency are separate ticket inputs and are not copied from the
current catalogue row.

The old reader can read both tickets because both writers populate
`product_code`. The new reader joins by `product_id` when present and falls
back to `product_code` only for pre-backfill or old-writer tickets.

### Mismatched references

Run the direct-write probe:

```bash
docker compose exec -T postgres psql -U mobility -d mobility \
  -v ON_ERROR_STOP=1 \
  < database/postgres/experiments/lecture04/mismatched_references.sql
```

The insert deliberately combines the `DAY` code with the ID of `SINGLE`, then
rolls the transaction back. The new writer prevents this mismatch by deriving
the code from the row selected by ID. The database currently does not prevent
a direct mismatch: `tickets_product_code_fk` and `tickets_product_id_fk`
validate each value independently, but no constraint requires both values to
identify the same product.

## ID-only reader and writer

The final application path is represented by:

- `database/postgres/experiments/lecture04/final_writer.sql`
- `database/postgres/experiments/lecture04/final_reader.sql`

The writer stores `product_id` without writing `tickets.product_code`. It still
stores the agreed price and currency directly on the ticket. The reader joins
`tickets` to `products` only through `product_id`; it obtains the business code
from `products.code`.

The earlier old and mixed-version scripts remain in the repository as rollout
evidence, not as the final application path. Initialisation files, the backfill
migration, and the pre-removal verification query also mention
`tickets.product_code` because they describe or verify the schema before
removal. No application source or automated test files exist in this starter
repository.

## Legacy-column removal rehearsal

`remove_legacy.sql` performs the following in one transaction:

1. Lists views and materialized views that depend on `tickets.product_code`.
2. Lists user-defined function bodies that mention `product_code`.
3. Refuses to continue if any ticket still needs its `product_id` backfilled.
4. Drops `tickets.product_code` without `CASCADE`.
5. Commits the removal.

Run it from the repository root:

```bash
docker compose exec -T postgres psql -U mobility -d mobility \
  -v ON_ERROR_STOP=1 \
  < database/postgres/experiments/lecture04/remove_legacy.sql
docker compose exec -T postgres psql -U mobility -d mobility \
  -v ON_ERROR_STOP=1 \
  < database/postgres/experiments/lecture04/final_writer.sql
docker compose exec -T postgres psql -U mobility -d mobility \
  -v ON_ERROR_STOP=1 \
  < database/postgres/experiments/lecture04/final_reader.sql
```

An empty result from both catalog queries means no matching view or function
was found. The uncascaded `ALTER TABLE` is the final database-side dependency
check: it fails rather than silently removing a dependent object. A successful
final insert and reader result show that the active SQL path no longer needs
the ticket column. Reset the disposable database if the compatibility schema
is needed again.
