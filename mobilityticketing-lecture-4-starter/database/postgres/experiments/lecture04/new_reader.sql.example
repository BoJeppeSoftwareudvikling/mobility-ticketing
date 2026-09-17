-- TODO: Extend this query to return a resolved_product_id.
-- Join by product_id when present; otherwise look up the product by code.
-- Before backfill, your query should still resolve every original ticket.
select t.id, t.product_code, t.product_id, t.price, t.currency
from tickets t
order by t.id;
