#!/usr/bin/env bash
# Installs deploy/k8s, the chart that `aspire publish` generates, in a throwaway kind cluster and checks that the
# application works there: the API answers and writes to PostgreSQL, and the reminders service reads from it.
# Needs Docker, kind, kubectl, helm and openssl. GitHub-hosted Ubuntu runners have all of them.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cluster="${KIND_CLUSTER:-entry-hub}"
tag="test"
port=18080

dump() {
  echo "----- pods -----"
  kubectl get pods -o wide || true
  for component in postgres api reminders; do
    echo "----- $component -----"
    kubectl describe pods -l "app.kubernetes.io/component=$component" | tail -n 30 || true
    kubectl logs -l "app.kubernetes.io/component=$component" --tail 100 --all-containers --prefix || true
  done
}
trap 'echo "The check failed. State of the cluster:"; dump' ERR

# The images the chart expects as parameters. The SDK builds them without a Dockerfile.
for project in Api Reminders; do
  name="entryhub-${project,,}"
  dotnet publish "$root/src/EntryHub.$project" -c Release --nologo -v quiet \
    -t:PublishContainer -p:ContainerRepository="$name" -p:ContainerImageTag="$tag"
done

if ! kind get clusters | grep -qx "$cluster"; then
  kind create cluster --name "$cluster" --wait 2m
fi
kind load docker-image "entryhub-api:$tag" "entryhub-reminders:$tag" --name "$cluster"

# Everything the chart leaves empty, generated for this run only. The signing key is 32 random bytes, base64 encoded.
password="$(openssl rand -hex 16)"
signing_key="$(openssl rand -base64 32)"
issuer="entryhub-k8s-test"
audience="entryhub-api"

helm upgrade --install entry-hub "$root/deploy/k8s" --wait --timeout 5m \
  --set parameters.api.api_image="entryhub-api:$tag" \
  --set parameters.reminders.reminders_image="entryhub-reminders:$tag" \
  --set secrets.postgres.postgres_password="$password" \
  --set secrets.api.postgres_password="$password" \
  --set secrets.reminders.postgres_password="$password" \
  --set secrets.api.jwt_signing_key="$signing_key" \
  --set config.api.jwt_issuer="$issuer" \
  --set config.api.jwt_audience="$audience"
# helm --wait is not enough: the chart's Deployments run one replica with maxUnavailable: 1, so Helm counts them
# ready with no pod ready at all. Wait for the pods themselves; the API's readiness probe checks PostgreSQL too.
kubectl wait pod -l app.kubernetes.io/component=api --for condition=Ready --timeout 3m
kubectl wait pod -l app.kubernetes.io/component=reminders --for condition=Ready --timeout 2m
# The API exits when PostgreSQL is not accepting connections yet, so a few restarts are expected here.
kubectl get pods

kubectl port-forward service/api-service "$port:8080" >/dev/null 2>&1 &
forward=$!
trap 'kill $forward 2>/dev/null || true' EXIT
for _ in $(seq 30); do
  curl -sf "http://localhost:$port/alive" >/dev/null && break
  kill -0 "$forward" 2>/dev/null || { echo "kubectl port-forward exited."; false; }
  sleep 1
done

base64url() { openssl base64 -A | tr '+/' '-_' | tr -d '='; }

# A token signed with the key the chart was installed with, the same shape `dotnet user-jwts` creates.
token() {
  local now header payload signature
  now="$(date +%s)"
  header="$(printf '{"alg":"HS256","typ":"JWT"}' | base64url)"
  payload="$(printf '{"sub":"%s","role":"%s","iss":"%s","aud":"%s","nbf":%d,"exp":%d}' \
    "$1" "$2" "$issuer" "$audience" "$now" "$((now + 600))" | base64url)"
  signature="$(printf '%s.%s' "$header" "$payload" \
    | openssl dgst -sha256 -mac HMAC -macopt "hexkey:$(printf '%s' "$signing_key" | openssl base64 -d -A | od -An -v -tx1 | tr -d " \n")" -binary \
    | base64url)"
  printf '%s.%s.%s' "$header" "$payload" "$signature"
}

echo "Probes:"
curl -sf "http://localhost:$port/health"; echo

echo "The API writes to PostgreSQL:"
room="$(curl -sf -X POST "http://localhost:$port/api/v1/rooms" \
  -H "Authorization: Bearer $(token facilities Facilities)" -H "Content-Type: application/json" \
  -d '{"name":"Kind room","floor":1,"capacity":6,"equipment":null}')"
echo "$room"
room_id="$(echo "$room" | sed -E 's/.*"id":"([^"]+)".*/\1/')"
curl -sf "http://localhost:$port/api/v1/rooms/$room_id" -H "Authorization: Bearer $(token facilities Facilities)" >/dev/null

# The booking rules only accept office hours, so the booking goes straight into the database, as in the system tests:
# what is under test is the reminders service finding it.
echo "The reminders service reads from PostgreSQL:"
kubectl exec postgres-statefulset-0 -- env PGPASSWORD="$password" psql -U postgres -d entryhub -v ON_ERROR_STOP=1 -c \
  "INSERT INTO bookings (\"Id\", \"RoomId\", \"OrganizerId\", \"Title\", \"StartAt\", \"EndAt\", \"Attendees\", \"Status\", \"CreatedAt\")
   VALUES (gen_random_uuid(), '$room_id', 'ana', 'Kind check', now() + interval '5 minutes', now() + interval '65 minutes', 4, 'Confirmed', now());"
for _ in $(seq 40); do
  if kubectl logs deployment/reminders-deployment | grep -q 'Reminder for ana: "Kind check"'; then
    kubectl logs deployment/reminders-deployment | grep 'Reminder for ana'
    echo "The application works in the cluster."
    exit 0
  fi
  sleep 3
done
echo "The reminders service did not remind the booking within two minutes."
false
