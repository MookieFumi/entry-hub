#!/usr/bin/env bash
# Deploys src/samples/graceful-shutdown in a kind cluster three times (without an orderly shutdown, with only the preStop
# wait, and with the whole orderly shutdown) and rolls out a new version of each under load. Each request leaves two
# jobs in memory; the script counts how many of them were processed and how many requests failed during the rollout.
# Needs Docker, kind and kubectl. GitHub-hosted Ubuntu runners have all of them.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
manifests="$root/src/samples/graceful-shutdown/k8s"
cluster="${KIND_CLUSTER:-entry-hub}"
work="$(mktemp -d)"
load_seconds=45
burst_after=9
burst_size=80
rollout_after=10

dotnet publish "$root/src/samples/graceful-shutdown/GracefulShutdown" -c Release --nologo -v quiet \
  -t:PublishContainer -p:ContainerImageTag=test

if ! kind get clusters | grep -qx "$cluster"; then
  kind create cluster --name "$cluster" --wait 2m
fi
kind load docker-image graceful-shutdown:test --name "$cluster"
kubectl apply -f "$manifests/common.yaml"

run() {
  local mode="$1" old
  echo "===== $mode ====="
  kubectl apply -f "$manifests/$mode.yaml"
  kubectl rollout status deployment/graceful-shutdown --timeout 2m
  kubectl delete pod load --ignore-not-found --wait
  kubectl exec deployment/graceful-shutdown -- rm -f /ledger/processed.log

  old="$(kubectl get pods -l app=graceful-shutdown -o jsonpath='{.items[0].metadata.name}')"
  kubectl logs -f --timestamps "$old" > "$work/$mode-old.log" 2>&1 &

  # Requests from inside the cluster, through the Service, as the clients of the API would send them: a steady stream
  # the pod keeps up with, and a burst just before the rollout that leaves a backlog of jobs in memory. Each line is the
  # job id and the status code; a request that does not get an answer is printed with code 000.
  kubectl run load --image curlimages/curl:8.11.1 --restart Never --command -- sh -c "
    post() { curl -s -X POST --max-time 3 -w ' %{http_code}\\n' http://graceful-shutdown/jobs || true; }
    start=\$(date +%s)
    burst=1
    while [ \$(date +%s) -lt \$((start + $load_seconds)) ]; do
      if [ \$burst = 1 ] && [ \$(date +%s) -ge \$((start + $burst_after)) ]; then
        for i in \$(seq $burst_size); do post & done
        wait
        burst=0
      fi
      post
      sleep 0.5
    done"
  kubectl wait pod/load --for condition=Ready --timeout 1m

  sleep "$rollout_after"
  kubectl rollout restart deployment/graceful-shutdown
  kubectl rollout status deployment/graceful-shutdown --timeout 2m
  kubectl wait "pod/$old" --for delete --timeout 2m

  kubectl wait pod/load --for jsonpath='{.status.phase}'=Succeeded --timeout 2m
  kubectl logs load > "$work/$mode-load.txt"

  # The new pod still has the end of the load queued: wait until the ledger stops growing.
  local previous=-1 current=0
  while [ "$current" != "$previous" ]; do
    previous="$current"
    sleep 3
    current="$(kubectl exec deployment/graceful-shutdown -- cat /ledger/processed.log | wc -l)"
  done
  kubectl exec deployment/graceful-shutdown -- cat /ledger/processed.log > "$work/$mode-ledger.txt"

  local accepted failed processed
  accepted="$(grep -c ' 202$' "$work/$mode-load.txt" || true)"
  failed="$(grep -vc ' 202$' "$work/$mode-load.txt" || true)"
  processed="$(wc -l < "$work/$mode-ledger.txt")"
  echo "Old pod:"
  grep -A1 'SIGTERM received\|Stopped' "$work/$mode-old.log" | grep -v '^--' || true
  echo "Requests: $((accepted + failed)), accepted: $accepted, failed: $failed"
  echo "Jobs: $((accepted * 2)) queued, $processed processed, $((accepted * 2 - processed)) lost"
  echo "$mode $accepted $failed $((accepted * 2)) $processed" >> "$work/summary.txt"
}

run naive
run prestop
run graceful

echo "===== summary ====="
printf '%-9s %9s %7s %12s %10s %5s\n' mode accepted failed jobs processed lost
while read -r mode accepted failed jobs processed; do
  printf '%-9s %9s %7s %12s %10s %5s\n' "$mode" "$accepted" "$failed" "$jobs" "$processed" "$((jobs - processed))"
done < "$work/summary.txt"

lost="$(awk '$1 == "graceful" { print $4 - $5 }' "$work/summary.txt")"
failed="$(awk '$1 == "graceful" { print $3 }' "$work/summary.txt")"
if [ "$lost" != 0 ] || [ "$failed" != 0 ]; then
  echo "With the orderly shutdown, the rollout should lose no jobs and fail no requests."
  false
fi
