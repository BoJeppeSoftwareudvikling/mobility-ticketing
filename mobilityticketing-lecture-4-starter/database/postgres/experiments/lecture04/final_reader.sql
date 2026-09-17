-- Resolve every ticket through product_id. products.code remains available as
-- the business-facing code, but it is no longer stored on tickets.
select t.id,
       t.product_id,
       p.code as product_code,
       t.price,
       t.currency
from tickets t
join products p on p.id = t.product_id
order by t.id;
