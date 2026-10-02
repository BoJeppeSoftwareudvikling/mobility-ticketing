-- Inspect the stored references after expansion.
select t.id, t.product_code, t.product_id, t.price, t.currency
from tickets t
order by t.id;

-- TODO: Join products and return only tickets with a null ID, a missing
-- product, or a code and ID that refer to different products.
-- Expect no rows after the final backfill. Test your check with a deliberate
-- mismatch inside a transaction, then roll it back.
