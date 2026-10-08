local value = redis.call('GET', KEYS[1])
if not value then return {err = 'availability unknown'} end
local current = tonumber(value)
if not current or current < 0 or current % 1 ~= 0 then
  return {err = 'invalid availability'}
end
if current == 0 then return -1 end
return redis.call('DECR', KEYS[1])
