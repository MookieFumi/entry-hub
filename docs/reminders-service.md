# The reminders service

`src/EntryHub.Reminders` is a separate .NET worker, not part of the API process. It has no HTTP endpoints. It reads and
writes the same PostgreSQL database as the API and does three jobs:

| Job | What it does | Settings (section `Reminders` or `Webhooks`) |
|---|---|---|
| Reminders | Reminds the organizer of a confirmed booking shortly before it starts. By default a reminder is a log line, see [notifications.md](notifications.md). | `Reminders:LeadTime` (15 min), `Reminders:PollInterval` (30 s), `Reminders:RetryAfter` (1 min) |
| Webhooks | Sends the events that the API saved with each booking change and keeps the result of every attempt. | `Webhooks:PollInterval` (5 s), `BatchSize` (20), `Concurrency` (4), `MaxAttempts` (6), `BaseDelay` (30 s, doubles), `Timeout` (10 s), `Lease` (2 min) |
| Auto-release | Cancels a confirmed booking that nobody checked in to within the minutes the tenant chose (`AutoReleaseMinutes`, off by default). | The tenant rules in the API |

Settings are ordinary .NET configuration: environment variables use `__` for the colon, for example
`Reminders__LeadTime=00:10:00`.

It is safe to run several copies: each reminder, delivery and release is reserved in the database before it is handled
(`FOR UPDATE SKIP LOCKED` plus a lease), so it happens once. It reads one tenant at a time and sets `app.tenant_id`
inside a transaction, so Row-Level Security applies to it like to the API.

## What it needs

- The connection string of the database in `ConnectionStrings__EntryHub`, with the same ordinary role as the API
  (`entryhub.<ref>` on Supabase, never the owner: the owner has `BYPASSRLS`). See
  [supabase-setup.md](supabase-setup.md) section 4. The API must have applied the migrations first.
- Outbound HTTPS, because it calls the URLs of the webhooks.
- Nothing else. SMTP is off unless you configure it, see [notifications.md](notifications.md).

## Building it

The project has no Dockerfile. Images come from the .NET SDK:

```bash
dotnet publish src/EntryHub.Reminders -c Release -t:PublishContainer
```

The `kubernetes` job of CI does this and installs the Helm chart (see the README), which already contains the service.
Without containers, `dotnet publish src/EntryHub.Reminders -c Release -o publish` gives files you can run with
`dotnet publish/EntryHub.Reminders.dll`.

## Running it in the demo (free Azure tier)

The demo API runs on App Service F1. That plan is not a good place for this service: it has no *Always On*, the app is
stopped after a few idle minutes, and it limits CPU time per day, so a worker that must look every few seconds would
stop most of the time. The options, from cheapest:

1. **A scheduled job (recommended for the demo).** Run the service in single-pass mode every few minutes from a GitHub
   Actions cron. It costs nothing and needs no Azure resource. See the next section.
2. **A container on Azure Container Apps (or any host that keeps a process running).** Run the image continuously with
   the connection string as a secret. Real time, but it is not free beyond the monthly allowance, so it is for later,
   together with the other things the project lists before the first paying customer.
3. **The same App Service plan with a paid tier** and *Always On*. Not worth it only for this.

### Single-pass mode

With `Reminders__RunOnce=true` (or `--Reminders:RunOnce=true`) the service does one pass of reminders, webhook
deliveries and auto-release, then exits. The exit code is 0 if it worked and 1 if any part failed (the other parts
still run), so the job turns red when something is wrong.

What changes compared with the continuous worker:

- A reminder, a webhook or an auto-release happens at the next pass, so up to one schedule interval late. GitHub does not
  schedule more often than every 5 minutes, and it can delay a run by several minutes when it is busy. Reminders sent
  15 minutes before the start still arrive in time with a 5 or 10 minute schedule.
- A failed webhook delivery is tried once per pass, so its six attempts take about six passes.
- GitHub turns scheduled workflows off after 60 days without repository activity, and may skip a run. Run it again by
  hand from *Actions* (see below) if you notice it stopped.

### Setting up the GitHub Actions job, in the browser

Nothing in the repository runs it yet. These steps add it. Create the secret first.

1. Open the repository on GitHub, *Settings → Secrets and variables → Actions → New repository secret*.
2. Name: `REMINDERS_CONNECTION_STRING`. Value: the connection string of the `entryhub.<ref>` role through the
   **session pooler** (the same one as the API setting `ConnectionStrings__EntryHub`, see
   [supabase-setup.md](supabase-setup.md)). GitHub runners reach Supabase over IPv4, so the pooler is required. Save.
3. *Code → Add file → Create new file*. Name it `.github/workflows/reminders.yml` and paste:

   ```yaml
   name: Reminders

   # One pass of the reminders service: reminders, webhook deliveries and auto-release.
   on:
     schedule:
       - cron: "*/10 * * * *"
     workflow_dispatch:

   permissions:
     contents: read

   concurrency:
     group: reminders
     cancel-in-progress: false

   jobs:
     pass:
       if: vars.REMINDERS_ENABLED == 'true'
       runs-on: ubuntu-latest
       timeout-minutes: 5
       steps:
         - uses: actions/checkout@v7
         - uses: actions/setup-dotnet@v6
           with:
             global-json-file: src/global.json
         - name: Run one pass
           env:
             ConnectionStrings__EntryHub: ${{ secrets.REMINDERS_CONNECTION_STRING }}
             Reminders__RunOnce: "true"
           run: dotnet run --project src/EntryHub.Reminders -c Release
   ```

4. Commit to `main` (through a pull request if the branch is protected).
5. *Settings → Secrets and variables → Actions → Variables → New repository variable*: `REMINDERS_ENABLED` with value
   `true`. Until then the job does nothing, like the `Deploy` workflow without `AZURE_WEBAPP_NAME`.
6. Try it: *Actions → Reminders → Run workflow*. A green run, whose log shows `Reminders: ... sent` when a booking was
   due, means it works.

The log of the free runner lives 90 days. The job does not need any other secret. Email stays off (no SMTP settings).

## Checklist before relying on it

- Webhooks and auto-release only work while a pass runs; with no schedule, they do nothing.
- Auto-release does not send the `booking.cancelled` webhook yet.
- The webhook secret is stored as text in the database.
