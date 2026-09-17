-- Start only after your ID-only reader and writer are ready.
begin;
set local lock_timeout = '3s';

-- TODO: Inspect views and functions that use tickets.product_code.
-- Update or remove those dependencies deliberately.
-- Drop the column without CASCADE and test your final reader and writer.

rollback;
-- Keep this rehearsal reversible. Record what would need to happen before
-- you committed the same change in a real rollout.
