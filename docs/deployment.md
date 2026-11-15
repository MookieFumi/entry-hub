# Deploying the demo to Azure App Service

The demo runs on an Azure App Service (free F1 plan) with PostgreSQL and sign-in from Supabase. Pushing a tag such as
`v1.0.0` builds the API, applies the migrations, deploys it and checks that it answers. This page lists what the
repository needs so that the `deploy` workflow can do that. The step-by-step setup of the cloud resources is in
`guia-setup-infra-demo.md` (project plan folder); here is only what GitHub and Azure must agree on.

## How a release goes

1. `git tag v1.0.0 && git push origin v1.0.0`.
2. **build**: runs the API tests, publishes the API and keeps it as an artifact for 30 days.
3. **deploy** (environment `production`, so it waits for the reviewers you set there):
   1. applies the migrations by running the published API with the `migrate` command and the owner connection string;
   2. logs in to Azure with a federated identity (no stored password) and deploys the artifact;
   3. smoke test: `GET /health` and `GET /api/v1/health` on the public URL, for up to two minutes;
   4. if the smoke test fails, starts the workflow again with the previous tag (rollback).

Nothing is deployed while the repository variable `AZURE_WEBAPP_NAME` is empty, so tags pushed before the cloud exists
do not fail.

To deploy a tag again by hand (or to go back): *Actions → deploy → Run workflow* and type the tag.

## Why the migrations run from the workflow

In every environment except Development the API does **not** change the schema when it starts. The workflow does it
with an owner role of the database, and the API connects with an ordinary role (no `BYPASSRLS`, no right to change
tables), which is what makes Row-Level Security protect the tenants. Migrations are not undone by the rollback: write
them so that the previous version keeps working on the new schema (add, do not remove or rename in the same release).

## Azure: federated login without secrets

Create an app registration that GitHub can sign in as, and give it rights over the web app only. With the Azure CLI,
replacing the values in capitals:

```bash
APP_ID=$(az ad app create --display-name entryhub-github --query appId -o tsv)
az ad sp create --id "$APP_ID"

# GitHub proves that the run belongs to this repository and to the "production" environment.
az ad app federated-credential create --id "$APP_ID" --parameters '{
  "name": "github-production",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:mookie-studio/entry-hub:environment:production",
  "audiences": ["api://AzureADTokenExchange"]
}'

# Contributor on the web app only, not on the subscription.
az role assignment create --assignee "$APP_ID" --role "Website Contributor" \
  --scope "$(az webapp show -g RESOURCE_GROUP -n WEBAPP_NAME --query id -o tsv)"
```

The `subject` must match the repository owner and name exactly; if the repository moves, create the credential again.

## GitHub: variables, secrets and environment

*Settings → Environments → New environment* named `production`; add the people who must approve a deployment as
*Required reviewers*, and restrict *Deployment branches and tags* to the tags `v*`.

| Where | Name | Kind | Value |
|---|---|---|---|
| Repository variable | `AZURE_WEBAPP_NAME` | variable | Name of the App Service. While empty, nothing is deployed |
| Repository variable | `API_URL` | variable | Public URL of the API without a trailing slash, for example `https://entryhub-demo.azurewebsites.net` |
| Repository variable | `AZURE_CLIENT_ID` | variable | `appId` of the app registration (not a secret) |
| Repository variable | `AZURE_TENANT_ID` | variable | Directory (tenant) id of Azure |
| Repository variable | `AZURE_SUBSCRIPTION_ID` | variable | Subscription id |
| Environment `production` | `MIGRATIONS_CONNECTION_STRING` | secret | Npgsql connection string of the **owner** role of the database (session pooler on Supabase) |

The API itself gets its settings in the App Service (*Settings → Environment variables*), not from GitHub:
`ConnectionStrings__EntryHub` with the **application** role, the `Authentication__*` settings of Supabase Auth, and
`Cors__AllowedOrigins__0` with the URL of the front end. Rate limiting and demo data have their own settings
(`RateLimiting__*`, `Demo__Seed`); see the README.

## Keeping Supabase awake

The free Supabase plan pauses a project after a week without activity. The `keep-alive` workflow calls
`GET /api/v1/health` every day at about 06:17 UTC; the call asks PostgreSQL, which counts as activity, and it also
wakes the App Service, which sleeps without Always On. It does nothing while `API_URL` is empty.
