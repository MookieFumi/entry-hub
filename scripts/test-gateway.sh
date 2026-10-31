#!/usr/bin/env bash
# Runs src/samples/gateway locally (no Docker): a stand-in legacy API, a stand-in new API and the YARP gateway in
# front of them. Checks which backend answers each route, moves a route to the new API by swapping the routes file
# while the gateway keeps running, and counts the 429 responses of the per-client rate limit.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
samples="$root/src/samples/gateway"
work="$(mktemp -d)"
pids=()
cleanup() { kill "${pids[@]}" 2>/dev/null || true; }
trap cleanup EXIT

for project in Gateway LegacyApi NewApi; do
  dotnet build "$samples/$project" --nologo -v quiet
done

cp "$samples/Gateway/routes.step1.json" "$work/routes.json"
dotnet run --no-build --project "$samples/NewApi" --urls http://localhost:5401 > "$work/new.log" 2>&1 &
pids+=($!)
dotnet run --no-build --project "$samples/LegacyApi" --urls http://localhost:5402 > "$work/legacy.log" 2>&1 &
pids+=($!)
dotnet run --no-build --project "$samples/Gateway" --urls http://localhost:5400 \
  --Gateway:RoutesFile="$work/routes.json" > "$work/gateway.log" 2>&1 &
pids+=($!)

for port in 5400 5401 5402; do
  for _ in $(seq 50); do
    curl -s -o /dev/null "http://localhost:$port/" && break
    sleep 0.2
  done
done

served_by() { curl -sf -D - -o /dev/null -H "X-Api-Key: ${2:-probe}" "http://localhost:5400$1" | tr -d '\r' | awk -F': ' 'tolower($1)=="x-served-by"{print $2}'; }
expect() {
  local path="$1" want="$2" got
  got="$(served_by "$path" "probe-$RANDOM")"
  echo "$path -> ${got:-none}"
  [ "$got" = "$want" ] || { echo "expected $want"; exit 1; }
}

echo "== Step 1: only orders are on the new API =="
expect /api/v1/orders new
expect /api/v1/customers/1 legacy

echo "== Step 2: customers move to the new API; the gateway is not restarted =="
cp "$samples/Gateway/routes.step2.json" "$work/routes.json"
for _ in $(seq 50); do
  [ "$(served_by /api/v1/customers/1 "probe-$RANDOM")" = "new" ] && break
  sleep 0.2
done
expect /api/v1/customers/1 new
expect /api/v1/orders new

echo "== Rate limit: 30 requests with one API key, then with another =="
count() {
  local ok=0 limited=0 code
  for _ in $(seq 30); do
    code="$(curl -s -o /dev/null -w '%{http_code}' -H "X-Api-Key: $1" http://localhost:5400/api/v1/orders)"
    if [ "$code" = 200 ]; then ok=$((ok + 1)); elif [ "$code" = 429 ]; then limited=$((limited + 1)); fi
  done
  echo "key $1: $ok x 200, $limited x 429"
  [ "$ok" = 20 ] && [ "$limited" = 10 ]
}
count client-a
count client-b
echo "OK"
