-- Sliding window rate limit (atomic). KEYS[1] key, ARGV: now_ms, window_ms, limit, member_id
local now = tonumber(ARGV[1])
local window = tonumber(ARGV[2])
local limit = tonumber(ARGV[3])
local member = ARGV[4]

redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, now - window)
local count = redis.call('ZCARD', KEYS[1])
if count >= limit then
  local oldest = redis.call('ZRANGE', KEYS[1], 0, 0, 'WITHSCORES')
  local retry_ms = window
  if oldest[2] ~= nil then
    retry_ms = window - (now - tonumber(oldest[2]))
    if retry_ms < 0 then
      retry_ms = 0
    end
  end
  return {0, retry_ms}
end
redis.call('ZADD', KEYS[1], now, member)
redis.call('PEXPIRE', KEYS[1], window + 1000)
return {1, 0}
