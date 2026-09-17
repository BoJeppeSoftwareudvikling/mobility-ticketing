-- Run only after all tickets have product_id and all deployed code uses it.
begin;
set local lock_timeout = '3s';

-- Views and materialized views that explicitly depend on the legacy column.
select view_schema, view_name
from information_schema.view_column_usage
where table_schema = 'public'
  and table_name = 'tickets'
  and column_name = 'product_code'
order by view_schema, view_name;

-- Function bodies that still mention the legacy column. The DROP below is the
-- authoritative dependency check for database objects tracked by PostgreSQL.
select n.nspname as function_schema,
       p.proname as function_name,
       pg_get_function_identity_arguments(p.oid) as arguments
from pg_proc p
join pg_namespace n on n.oid = p.pronamespace
where n.nspname not in ('pg_catalog', 'information_schema')
  and p.prokind in ('f', 'p')
  and p.prosrc ilike '%product_code%'
order by function_schema, function_name, arguments;

do $$
begin
    if exists (select 1 from tickets where product_id is null) then
        raise exception 'Cannot remove product_code: tickets still need backfill';
    end if;
end
$$;

-- Do not use CASCADE. PostgreSQL must report any dependency not handled above.
alter table tickets drop column product_code;

commit;
