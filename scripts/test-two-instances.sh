#!/usr/bin/env bash
# Runs src/samples/two-instances locally (no Docker): two instances of ProductsApi behind a YARP balancer, and
# measures what changes when an API that works with one instance runs as two.
#   1. In-memory cache: after a price change one instance keeps answering with the old price.
#   2. Redis cache: both instances answer with the new price. Needs redis-server; skipped when it is not installed.
#   3. Session affinity: the client that made the change sees it, the other client keeps seeing the old price.
#   4. Rate limit: the built-in limiter counts per instance (two instances double the limit); in the balancer it holds.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
samples="$root/src/samples/two-instances"
work="$(mktemp -d)"
pids=()
stack_pids=()
redis_port=6390

stop_stack() {
  if [ "${#stack_pids[@]}" -gt 0 ]; then
    kill "${stack_pids[@]}" 2>/dev/null || true
    wait "${stack_pids[@]}" 2>/dev/null || true
  fi
  stack_pids=()
}
cleanup() { stop_stack; if [ "${#pids[@]}" -gt 0 ]; then kill "${pids[@]}" 2>/dev/null || true; fi; }
trap cleanup EXIT

for project in Balancer ProductsApi; do
  dotnet build "$samples/$project" --nologo -v quiet
done

# Starts two instances and the balancer. $1: routes file, $2: cache mode, $3: whether the instances limit requests.
start_stack() {
  stop_stack
  rm -f "$work/products.json"
  for n in 1 2; do
    Instance__Name="instance-$n" Store__Path="$work/products.json" Cache__Mode="$2" \
      Cache__Redis="localhost:$redis_port" RateLimit__Enabled="$3" \
      dotnet run --no-build --project "$samples/ProductsApi" --urls "http://localhost:550$n" \
      > "$work/instance-$n.log" 2>&1 &
    stack_pids+=($!)
  done
  dotnet run --no-build --project "$samples/Balancer" --urls http://localhost:5500 --Balancer:RoutesFile="$1" \
    > "$work/balancer.log" 2>&1 &
  stack_pids+=($!)
  for port in 5500 5501 5502; do
    for _ in $(seq 100); do
      curl -s -o /dev/null "http://localhost:$port/" && break
      sleep 0.2
    done
  done
}

get_price() { # $1: cookie jar, $2: API key
  curl -s -b "$1" -c "$1" -H "X-Api-Key: $2" http://localhost:5500/products/1 | sed -E 's/.*"price":([0-9.]+).*/\1/'
}
set_price() { curl -s -o /dev/null -b "$1" -c "$1" -X PUT "http://localhost:5500/products/1/price/$2"; }
tally() { # $1: cookie jar, $2: how many requests, $3: the old price; prints "<old> <new>"
  local old=0 new=0 price
  for _ in $(seq "$2"); do
    price="$(get_price "$1" tally)"
    if [ "$price" = "$3" ]; then old=$((old + 1)); else new=$((new + 1)); fi
  done
  echo "$old $new"
}

echo "== 1. In-memory cache, two instances, round robin =="
start_stack balancer.round-robin.json memory false
jar="$work/jar-1"; : > "$jar"
for _ in 1 2 3 4; do get_price "$jar" prime > /dev/null; done
set_price "$jar" 20
read -r old new <<< "$(tally "$jar" 10 10)"
echo "10 reads after the price changed from 10 to 20: $old with the old price, $new with the new one"
[ "$old" -gt 0 ] && [ "$new" -gt 0 ]

echo "== 2. Redis cache, two instances, round robin =="
if command -v redis-server > /dev/null; then
  redis-server --port "$redis_port" --save "" --appendonly no > "$work/redis.log" 2>&1 &
  pids+=($!)
  for _ in $(seq 50); do (echo > "/dev/tcp/localhost/$redis_port") 2> /dev/null && break; sleep 0.1; done
  start_stack balancer.round-robin.json redis false
  for _ in 1 2 3 4; do get_price "$jar" prime > /dev/null; done
  set_price "$jar" 20
  read -r old new <<< "$(tally "$jar" 10 10)"
  echo "10 reads after the price changed from 10 to 20: $old with the old price, $new with the new one"
  [ "$old" -eq 0 ] && [ "$new" -eq 10 ]
else
  echo "SKIPPED: redis-server is not installed"
fi

echo "== 3. In-memory cache with session affinity =="
start_stack balancer.affinity.json memory false
jar_a="$work/jar-a"; : > "$jar_a"
jar_b="$work/jar-b"; : > "$jar_b"
get_price "$jar_a" prime > /dev/null
get_price "$jar_b" prime > /dev/null
set_price "$jar_a" 20
read -r old new <<< "$(tally "$jar_a" 10 10)"
echo "client that changed the price: $old reads with the old price, $new with the new one"
[ "$old" -eq 0 ]
read -r old new <<< "$(tally "$jar_b" 10 10)"
echo "another client:                $old reads with the old price, $new with the new one"
[ "$new" -eq 0 ]

count_status() { # $1: API key; prints "<200s> <429s>" for 30 requests
  local ok=0 limited=0 code
  for _ in $(seq 30); do
    code="$(curl -s -o /dev/null -w '%{http_code}' -H "X-Api-Key: $1" http://localhost:5500/products/1)"
    if [ "$code" = 200 ]; then ok=$((ok + 1)); elif [ "$code" = 429 ]; then limited=$((limited + 1)); fi
  done
  echo "$ok $limited"
}

echo "== 4a. Limit of 20 per minute in each instance (built-in rate limiter) =="
start_stack balancer.round-robin.json memory true
read -r ok limited <<< "$(count_status limit-a)"
echo "30 requests with one API key: $ok x 200, $limited x 429"
[ "$ok" -eq 30 ]

echo "== 4b. Limit of 20 per minute in the balancer =="
start_stack balancer.limit.json memory false
read -r ok limited <<< "$(count_status limit-b)"
echo "30 requests with one API key: $ok x 200, $limited x 429"
[ "$ok" -eq 20 ] && [ "$limited" -eq 10 ]
echo "OK"
