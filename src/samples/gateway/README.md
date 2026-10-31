# Gateway: migrating from a legacy API without the clients noticing

Three small projects. Nothing in `EntryHub.Api` uses them.

| Project | Port | What it is |
|---|---|---|
| `LegacyApi` | 5402 | Stands in for the old system: `/orders` and `/customers/{id}`, upper-case fields (`ORDER_ID`), no versioning |
| `NewApi` | 5401 | Stands in for the new API: `/api/v1/orders` and `/api/v1/customers/{id}`, camelCase fields |
| `Gateway` | 5400 | YARP in front of both. Clients only know `http://localhost:5400/api/v1/...` |

Every response carries `X-Served-By` (`legacy` or `new`), so it is visible which backend answered.

## The migration is a change to one file

`Gateway/routes.step1.json` sends `/api/v1/orders` to the new API and everything else under `/api/v1` to the legacy one
(removing the `/api/v1` prefix on the way). `routes.step2.json` also moves `/api/v1/customers`. The gateway watches the
routes file, so swapping one for the other moves the route without restarting anything.

The gateway also applies a rate limit of 20 requests per minute per `X-Api-Key`. The count lives in the gateway: the
services behind it never see the excess requests.

## Run it

```bash
scripts/test-gateway.sh
```

It starts the three projects, checks which backend answers each route, swaps the routes file while the gateway keeps
running, and counts the `200` and `429` responses of two API keys. It runs in the `gateway` job of the CI.

To try it by hand:

```bash
dotnet run --project samples/gateway/NewApi --urls http://localhost:5401 &
dotnet run --project samples/gateway/LegacyApi --urls http://localhost:5402 &
dotnet run --project samples/gateway/Gateway --urls http://localhost:5400
curl -i http://localhost:5400/api/v1/customers/1    # X-Served-By: legacy
# Start the gateway with --Gateway:RoutesFile=routes.step2.json and the same request answers `new`.
```
