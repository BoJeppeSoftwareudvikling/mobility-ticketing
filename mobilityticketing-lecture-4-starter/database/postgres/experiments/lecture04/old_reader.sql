-- This represents application code that still reads by product_code.
select id, product_code, price, currency
from tickets
order by id;
