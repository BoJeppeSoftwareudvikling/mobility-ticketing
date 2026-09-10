# Lecture 3 - Daily Captured Revenue

## What I am testing

In this lab I tested four different ways to show daily captured revenue in MobilityTicketing.

- a normal SQL query
- a SQL function
- a materialized view
- a summary table maintained by a trigger

The important question is not just which one works. The important question is where the responsibility should be placed.

Should captured revenue be calculated from the real payment data when we need it, or should the database automatically maintain a separate summary table through a trigger?

For this experiment I treat `payments` as the source of truth. Everything else is derived from `payments`.

---

## Source of truth

Captured revenue comes from the `payments` table.

A payment should only count as revenue when the payment has status `Captured`.

To find the operator, the query has to follow this path:

```text
payments -> tickets -> trips -> routes -> operators
```

That means daily revenue is not its own independent fact. It depends on the current state of the payment rows.

---

## Direct query

The direct query calculates the report from the base tables every time it runs.

```sql
select
    r.operator_id,
    p.created_utc::date as revenue_date,
    sum(p.amount) as captured_amount,
    count(*) as captured_payments
from payments p
join tickets t on t.id = p.ticket_id
join trips tr on tr.id = t.trip_id
join routes r on r.id = tr.route_id
where p.status = 'Captured'
group by r.operator_id, p.created_utc::date
order by r.operator_id, p.created_utc::date;
```

Baseline output:

```text
 operator_id | revenue_date | captured_amount | captured_payments 
-------------+--------------+-----------------+-------------------
 OP-BUS      | 2026-04-29   |           36.00 |                 1
 OP-METRO    | 2026-04-29   |           36.00 |                 1
(2 rows)
```

This is the cleanest reference result because it reads the current data directly from `payments`. It does not store a second copy of the result.

---

## Checking the base rows

I also checked the base rows behind the result.

```sql
select
    p.id,
    p.status,
    p.amount,
    p.created_utc::date as revenue_date,
    t.id as ticket_id,
    tr.id as trip_id,
    r.operator_id
from payments p
join tickets t on t.id = p.ticket_id
join trips tr on tr.id = t.trip_id
join routes r on r.id = tr.route_id
order by p.id;
```

Output:

```text
    id     |  status  | amount | revenue_date | ticket_id |        trip_id        | operator_id 
-----------+----------+--------+--------------+-----------+-----------------------+-------------
 PAYMENT-1 | Captured |  36.00 | 2026-04-29   | TICKET-1  | TRIP-M2-20260429-0800 | OP-METRO
 PAYMENT-2 | Captured |  36.00 | 2026-04-29   | TICKET-2  | TRIP-5C-20260429-0900 | OP-BUS
(2 rows)
```

This shows which payments are behind the revenue result and which operator each payment belongs to.

---

## SQL function

The function wraps the same read logic in a named database object.

```sql
create or replace function captured_revenue_for_day(
    requested_operator_id text,
    requested_date date
)
returns table (
    captured_amount numeric,
    captured_payments bigint
)
language sql
stable
as $$
    select
        coalesce(sum(p.amount), 0),
        count(*)
    from payments p
    join tickets t on t.id = p.ticket_id
    join trips tr on tr.id = t.trip_id
    join routes r on r.id = tr.route_id
    where r.operator_id = requested_operator_id
      and p.created_utc::date = requested_date
      and p.status = 'Captured';
$$;
```

Example call:

```sql
select *
from captured_revenue_for_day('OP-METRO', date '2026-04-29');
```

Output:

```text
 captured_amount | captured_payments 
-----------------+-------------------
           36.00 |                 1
(1 row)
```

The function does not store data. It reads from `payments` when it is called, so it stays current like the direct query.

The benefit is that the reporting logic has a name and can be reused. The downside is that the logic now lives inside the database and must be maintained with the schema.

---

## Materialized view

The materialized view stores the result of the revenue query.

```sql
create materialized view daily_captured_revenue as
select
    r.operator_id,
    p.created_utc::date as revenue_date,
    sum(p.amount) as captured_amount,
    count(*) as captured_payments
from payments p
join tickets t on t.id = p.ticket_id
join trips tr on tr.id = t.trip_id
join routes r on r.id = tr.route_id
where p.status = 'Captured'
group by r.operator_id, p.created_utc::date
with no data;

create unique index daily_captured_revenue_key
    on daily_captured_revenue (operator_id, revenue_date);
```

I had to refresh it before reading from it.

```sql
refresh materialized view daily_captured_revenue;
```

Output after refresh:

```text
 operator_id | revenue_date | captured_amount | captured_payments 
-------------+--------------+-----------------+-------------------
 OP-BUS      | 2026-04-29   |           36.00 |                 1
 OP-METRO    | 2026-04-29   |           36.00 |                 1
(2 rows)
```

This approach makes reads cheaper because the result is already stored. The problem is that it can become old. When `payments` changes, the materialized view does not update by itself.

---

## Trigger summary

The trigger approach stores daily revenue in `daily_revenue_by_operator`.

When a new captured payment is inserted, the trigger updates the summary table.

```sql
create table daily_revenue_by_operator (
    operator_id text not null references operators(id),
    revenue_date date not null,
    captured_amount numeric not null default 0,
    captured_payments bigint not null default 0,
    primary key (operator_id, revenue_date)
);

create or replace function add_inserted_payment_to_daily_revenue()
returns trigger
language plpgsql
as $$
declare
    payment_operator_id text;
begin
    if new.status is distinct from 'Captured' then
        return new;
    end if;

    select r.operator_id
    into payment_operator_id
    from tickets t
    join trips tr on tr.id = t.trip_id
    join routes r on r.id = tr.route_id
    where t.id = new.ticket_id;

    insert into daily_revenue_by_operator (
        operator_id, revenue_date, captured_amount, captured_payments
    ) values (
        payment_operator_id, new.created_utc::date, new.amount, 1
    )
    on conflict (operator_id, revenue_date)
    do update set
        captured_amount = daily_revenue_by_operator.captured_amount + excluded.captured_amount,
        captured_payments = daily_revenue_by_operator.captured_payments + 1;

    return new;
end;
$$;

create trigger payments_daily_revenue_after_insert
after insert on payments
for each row
execute function add_inserted_payment_to_daily_revenue();
```

Output immediately after creating the summary table and trigger:

```text
 operator_id | revenue_date | captured_amount | captured_payments 
-------------+--------------+-----------------+-------------------
(0 rows)
```

The summary table starts empty unless we backfill it. That is important. The trigger only sees future inserts. It does not know about payments that were already in the database.

---

## Test case 1 - Captured payment insert

SQL used:

```sql
insert into payments (
    id, user_id, ticket_id, external_payment_reference,
    amount, currency, status, created_utc
) values (
    'PAY-CASE-CAPTURED', 'USER-1', 'TICKET-1', 'gateway-case-captured',
    36, 'DKK', 'Captured', '2026-04-29 10:00:00+00'
);
```

Output after the insert:

```text
     approach      | operator_id | revenue_date | captured_amount | captured_payments 
-------------------+-------------+--------------+-----------------+-------------------
 direct query      | OP-BUS      | 2026-04-29   |           36.00 |                 1
 direct query      | OP-METRO    | 2026-04-29   |           72.00 |                 2
 materialized view | OP-BUS      | 2026-04-29   |           36.00 |                 1
 materialized view | OP-METRO    | 2026-04-29   |           36.00 |                 1
 sql function      | OP-BUS      | 2026-04-29   |           36.00 |                 1
 sql function      | OP-METRO    | 2026-04-29   |           72.00 |                 2
 trigger summary   | OP-METRO    | 2026-04-29   |              36 |                 1
(7 rows)
```

What happened:

The direct query and the function saw the new captured payment right away.

The materialized view did not see it yet because it had not been refreshed.

The trigger summary changed, but only for the new insert. It still did not include old seeded payments unless a backfill was done.

---

## Test case 2 - Failed payment insert

SQL used:

```sql
insert into payments (
    id, user_id, ticket_id, external_payment_reference,
    amount, currency, status, created_utc
) values (
    'PAY-CASE-FAILED', 'USER-1', 'TICKET-1', 'gateway-case-failed',
    50, 'DKK', 'Failed', '2026-04-29 10:05:00+00'
);
```

Output after the insert:

```text
     approach      | operator_id | revenue_date | captured_amount | captured_payments 
-------------------+-------------+--------------+-----------------+-------------------
 direct query      | OP-BUS      | 2026-04-29   |           36.00 |                 1
 direct query      | OP-METRO    | 2026-04-29   |           72.00 |                 2
 materialized view | OP-BUS      | 2026-04-29   |           36.00 |                 1
 materialized view | OP-METRO    | 2026-04-29   |           36.00 |                 1
 sql function      | OP-BUS      | 2026-04-29   |           36.00 |                 1
 sql function      | OP-METRO    | 2026-04-29   |           72.00 |                 2
 trigger summary   | OP-METRO    | 2026-04-29   |              36 |                 1
(7 rows)
```

What happened:

The failed payment did not count as captured revenue. That is correct because only payments with status `Captured` should be counted.

---

## Test case 3 - Failed changed to Captured

SQL used:

```sql
update payments
set status = 'Captured'
where id = 'PAY-CASE-FAILED';
```

Output after the update:

```text
     approach      | operator_id | revenue_date | captured_amount | captured_payments 
-------------------+-------------+--------------+-----------------+-------------------
 direct query      | OP-BUS      | 2026-04-29   |           36.00 |                 1
 direct query      | OP-METRO    | 2026-04-29   |          122.00 |                 3
 materialized view | OP-BUS      | 2026-04-29   |           36.00 |                 1
 materialized view | OP-METRO    | 2026-04-29   |           36.00 |                 1
 sql function      | OP-BUS      | 2026-04-29   |           36.00 |                 1
 sql function      | OP-METRO    | 2026-04-29   |          122.00 |                 3
 trigger summary   | OP-METRO    | 2026-04-29   |              36 |                 1
(7 rows)
```

What happened:

The direct query and the function included the payment after the status changed.

The trigger summary did not update because the trigger only runs on `INSERT`. It does not run on `UPDATE`.

The materialized view was still old until refresh.

---

## Test case 4 - Captured changed to Refunded

SQL used:

```sql
update payments
set status = 'Refunded'
where id = 'PAY-CASE-CAPTURED';
```

Output after the update:

```text
     approach      | operator_id | revenue_date | captured_amount | captured_payments 
-------------------+-------------+--------------+-----------------+-------------------
 direct query      | OP-BUS      | 2026-04-29   |           36.00 |                 1
 direct query      | OP-METRO    | 2026-04-29   |           86.00 |                 2
 materialized view | OP-BUS      | 2026-04-29   |           36.00 |                 1
 materialized view | OP-METRO    | 2026-04-29   |           36.00 |                 1
 sql function      | OP-BUS      | 2026-04-29   |           36.00 |                 1
 sql function      | OP-METRO    | 2026-04-29   |           86.00 |                 2
 trigger summary   | OP-METRO    | 2026-04-29   |              36 |                 1
(7 rows)
```

What happened:

The direct query and the function stopped counting the payment because it was no longer captured.

The trigger summary did not subtract it because the trigger does not handle updates.

The materialized view was still old until refresh.

---

## Test case 5 - Delete test data

SQL used:

```sql
delete from payments
where id = 'PAY-CASE-FAILED';
```

Output after the delete:

```text
     approach      | operator_id | revenue_date | captured_amount | captured_payments 
-------------------+-------------+--------------+-----------------+-------------------
 direct query      | OP-BUS      | 2026-04-29   |           36.00 |                 1
 direct query      | OP-METRO    | 2026-04-29   |           36.00 |                 1
 materialized view | OP-BUS      | 2026-04-29   |           36.00 |                 1
 materialized view | OP-METRO    | 2026-04-29   |           36.00 |                 1
 sql function      | OP-BUS      | 2026-04-29   |           36.00 |                 1
 sql function      | OP-METRO    | 2026-04-29   |           36.00 |                 1
 trigger summary   | OP-METRO    | 2026-04-29   |              36 |                 1
(7 rows)
```

What happened:

The direct query and the function no longer counted the deleted payment.

The trigger summary did not subtract it because the trigger does not handle deletes.

The materialized view was still old until refresh.

---

## Test case 6 - Duplicate external payment reference

SQL used:

```sql
insert into payments (
    id, user_id, ticket_id, external_payment_reference,
    amount, currency, status, created_utc
) values (
    'PAY-CASE-DUPLICATE', 'USER-1', 'TICKET-1', 'gateway-capture-0001',
    36, 'DKK', 'Captured', '2026-04-29 10:10:00+00'
);
```

Output after the insert:

```text
     approach      | operator_id | revenue_date | captured_amount | captured_payments 
-------------------+-------------+--------------+-----------------+-------------------
 direct query      | OP-BUS      | 2026-04-29   |           36.00 |                 1
 direct query      | OP-METRO    | 2026-04-29   |           72.00 |                 2
 materialized view | OP-BUS      | 2026-04-29   |           36.00 |                 1
 materialized view | OP-METRO    | 2026-04-29   |           36.00 |                 1
 sql function      | OP-BUS      | 2026-04-29   |           36.00 |                 1
 sql function      | OP-METRO    | 2026-04-29   |           72.00 |                 2
 trigger summary   | OP-METRO    | 2026-04-29   |              72 |                 2
(7 rows)
```

What happened:

The duplicate payment reference was accepted. The starter schema does not have a unique constraint on `external_payment_reference`.

Because of that, the direct query and the function counted it as another captured payment. The trigger summary also counted it. The materialized view only included it after refresh.

---

## Example where the approaches disagreed

The clearest disagreement happened after inserting `PAY-CASE-CAPTURED`.

The direct query and the function showed the new payment immediately.

The materialized view still showed the old number because it had not been refreshed.

The trigger summary also disagreed with the full base-table result because it only had rows created by the trigger after the trigger was installed. It did not include the payments from the seed data.

Evidence:

```text
     approach      | operator_id | revenue_date | captured_amount | captured_payments 
-------------------+-------------+--------------+-----------------+-------------------
 direct query      | OP-BUS      | 2026-04-29   |           36.00 |                 1
 direct query      | OP-METRO    | 2026-04-29   |           72.00 |                 2
 materialized view | OP-BUS      | 2026-04-29   |           36.00 |                 1
 materialized view | OP-METRO    | 2026-04-29   |           36.00 |                 1
 sql function      | OP-BUS      | 2026-04-29   |           36.00 |                 1
 sql function      | OP-METRO    | 2026-04-29   |           72.00 |                 2
 trigger summary   | OP-METRO    | 2026-04-29   |              36 |                 1
(7 rows)
```

This is the main lesson for me. Stored reporting copies can be useful, but they need clear rules for refresh, backfill, corrections, and rebuild.

---

## Side-effect trace

One captured payment insert caused more than one thing to happen.

The sequence was:

1. A row was inserted into `payments`.
2. PostgreSQL checked the primary key on `payments.id`.
3. The trigger `payments_daily_revenue_after_insert` fired.
4. The trigger read `tickets`, `trips`, and `routes` to find the operator.
5. The trigger inserted or updated `daily_revenue_by_operator`.
6. The payment insert and summary update were part of the same transaction.
7. When the transaction was rolled back, both changes disappeared.

Evidence:

```text
Insert on public.payments
  Buffers: shared hit=3
  ->  Result
        Output: 'PAY-TRACE-CAPTURED'::text, 'USER-1'::text, 'TICKET-1'::text, 'gateway-trace-captured'::text, '36'::numeric, 'DKK'::text, 'Captured'::text, '2026-04-29 11:00:00+00'::timestamp with time zone
Planning Time: 0.030 ms
Trigger payments_daily_revenue_after_insert: time=0.455 calls=1
Execution Time: 0.505 ms

         id         | user_id | ticket_id | external_payment_reference | amount | currency |  status  |      created_utc       
--------------------+---------+-----------+----------------------------+--------+----------+----------+------------------------
 PAY-TRACE-CAPTURED | USER-1  | TICKET-1  | gateway-trace-captured     |     36 | DKK      | Captured | 2026-04-29 11:00:00+00
(1 row)

 operator_id | revenue_date | captured_amount | captured_payments 
-------------+--------------+-----------------+-------------------
 OP-METRO    | 2026-04-29   |              36 |                 1
(1 row)
```

This shows the hidden side effect. Code that inserts a payment also changes reporting data, even if the application did not explicitly ask for a reporting update.

---

## Responsibility matrix

| Approach | Correctness | Freshness | Write cost | Read cost | Hidden side effects | Rebuildability | Operational complexity |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Direct SQL query | Correct when `payments` is correct | Current when read | No extra write cost | Higher because it aggregates each time | None | Easy because nothing is stored | Low |
| SQL function | Same as direct query | Current when called | No extra write cost | Similar to direct query | Low because it has no writes | Easy because nothing is stored | Low to medium |
| Materialized view | Correct only after refresh | Old until refreshed | No extra payment write cost | Low after refresh | No write side effect, but stale reads are possible | Rebuilt with `REFRESH MATERIALIZED VIEW daily_captured_revenue` | Medium |
| Trigger summary | Only correct for the cases the trigger handles | Fresh for captured inserts after the trigger exists | Higher because payment insert also updates summary | Low | High because writes happen automatically | Needs a separate backfill and rebuild process | High |

---

## Recommendation

I would keep `payments` as the authority for captured revenue.

I would not make `daily_revenue_by_operator` the authoritative source for captured revenue in the current version.

The trigger approach is attractive because reads become simple and fast. But the experiment shows the cost. The database starts doing extra work automatically when a payment is inserted. That behavior is easy to miss if someone only looks at the application code.

The trigger also does not handle several realistic cases in the starter version:

- payments that existed before the trigger was created
- failed payments that later become captured
- captured payments that later become refunded
- deleted or replaced payments
- duplicate external payment references

For the current workload, I would use the direct query as the safest reference. If the same reporting logic needs to be reused, I would expose it through the SQL function.

If daily reporting later becomes read-heavy, I would consider the materialized view as a hybrid. In that case `payments` would still be the authority, and the materialized view would need a clear refresh rule.

I would only choose the trigger summary if the team is ready to own the full complexity: update handling, delete handling, refund handling, duplicate handling, backfill, monitoring, and rebuild.

---

## Optional rebuild path

If the summary table is kept, it needs a way to be rebuilt from `payments`.

```sql
truncate table daily_revenue_by_operator;

insert into daily_revenue_by_operator (
    operator_id,
    revenue_date,
    captured_amount,
    captured_payments
)
select
    r.operator_id,
    p.created_utc::date as revenue_date,
    sum(p.amount) as captured_amount,
    count(*) as captured_payments
from payments p
join tickets t on t.id = p.ticket_id
join trips tr on tr.id = t.trip_id
join routes r on r.id = tr.route_id
where p.status = 'Captured'
group by r.operator_id, p.created_utc::date;
```

This helps recovery, but it does not remove the complexity of keeping a stored summary correct over time.
