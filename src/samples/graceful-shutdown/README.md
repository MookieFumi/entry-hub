# Graceful shutdown: what the old pod leaves behind in a rollout

A Minimal API where each `POST /jobs` leaves two jobs in memory, one for each of two background services
(`ConfirmationWorker` and `AuditWorker`), and answers `202` before they run. Each job takes 300 ms and writes one line
to a ledger file. Nothing in `EntryHub.Api` uses it.

`scripts/test-graceful-shutdown.sh` deploys it in kind three times and rolls out a new version under load each time.
The load is a steady stream the pod keeps up with, plus a burst of 80 requests just before the rollout, which leaves
about 24 seconds of jobs queued in the old pod.

| | `k8s/naive.yaml` | `k8s/prestop.yaml` | `k8s/graceful.yaml` |
|---|---|---|---|
| Worker | `Jobs__Drain=false`: the stopping token goes to the job, so the job in progress is cancelled and the queue is left behind | `Jobs__Drain=false` | `Jobs__Drain=true`: the job gets `CancellationToken.None` and the worker returns once the queue is empty |
| `preStop` | none | `sleep: 10`, so the pod is out of the Service before it gets SIGTERM | `sleep: 10` |
| `HostOptions.ShutdownTimeout` | 30 s (the default) | 45 s | 45 s |
| `terminationGracePeriodSeconds` | 30 s (the default) | 60 s | 60 s |

The ledger lives on a volume both pods mount, so the jobs of the old pod can still be counted after it is gone. The
script compares the jobs the accepted requests queued with the lines in the ledger, and fails if the graceful
rollout loses any job or request. It runs in the `kubernetes` job of the CI.

What the run shows, and why:

- The host stops its hosted services in reverse order of registration, and the web server is registered last, so it
  stops first: when the workers are told to stop, no request can queue anything else.
- `ChannelReader.ReadAllAsync` keeps handing out the items already queued after its token is cancelled and stops only
  when the queue is empty. Whether the queue is drained depends on the token the job gets, not on the loop.
- `AddHostedService` registers each implementation type once: two `AddHostedService` calls with factories of the same
  class start a single worker. That is why each queue has its own class.

Locally, without Kubernetes:

```bash
Jobs__Drain=true dotnet run --project samples/graceful-shutdown/GracefulShutdown --urls http://localhost:5390
for i in $(seq 20); do curl -s -X POST localhost:5390/jobs; echo; done
# Ctrl+C right after: the log says how many queued jobs each worker processed before stopping.
```
