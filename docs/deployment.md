# Deploying the demo to Azure App Service

The demo runs on an Azure App Service (free F1 plan) with PostgreSQL and sign-in from Supabase. Pushing a tag such as
`v1.0.0` builds the API, applies the migrations, deploys it and checks that it answers. This page lists what the
repository needs so that the `deploy` workflow can do that. The step-by-step setup of the cloud resources is in
[supabase-setup.md](supabase-setup.md) (database roles, sign-in hook, demo users); here is only what GitHub and Azure
must agree on.

Every step has two ways: the Azure CLI commands, and the same thing in the browser (Azure portal and the GitHub web).
The menu names in the browser steps were written from memory of the current interfaces and have not been checked
against live accounts; the portals change often, so if a menu is missing, use the search box of the portal.

The reminders service (reminders, webhooks, auto-release) is a separate worker that this workflow does not deploy; see
[reminders-service.md](reminders-service.md) for how to run it next to the demo.

## How a release goes

1. Create the tag `v1.0.0`: `git tag v1.0.0 && git push origin v1.0.0`, or in GitHub *Releases → Draft a new release →
   Choose a tag → Create new tag on publish → Publish release* (the `release` workflow attaches the contract to a
   release that already exists).
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

Create an app registration that GitHub can sign in as, and give it rights over the web app only.

**Azure CLI**, replacing the values in capitals:

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

**Azure portal** (https://portal.azure.com):

1. *Microsoft Entra ID → App registrations → New registration*: name `entryhub-github`, *Single tenant only*, no
   redirect URI, *Register*. Copy **Application (client) ID** (this is `AZURE_CLIENT_ID`) and **Directory (tenant) ID**
   (`AZURE_TENANT_ID`) from the *Overview*.
2. In that app: *Certificates & secrets → Federated credentials → Add credential*. Scenario *GitHub Actions deploying
   Azure resources*, organization `mookie-studio`, repository `entry-hub`, entity type *Environment*, environment name
   `production`, name `github-production`, *Add*. If the form asks for numeric ids instead, see the next section.
3. Open the App Service: *Access control (IAM) → Add → Add role assignment*. Role *Website Contributor* (under *Job
   function roles*), members: *User, group, or service principal → Select members → `entryhub-github`*, then *Review +
   assign*.
4. *Subscriptions →* your subscription: copy the **Subscription ID** (`AZURE_SUBSCRIPTION_ID`).

The `subject` must match the repository owner and name exactly; if the repository moves, create the credential again.

### When the credential form asks for GitHub numeric ids

Some versions of the form ask for the **organization id** and the **repository id** of GitHub. You can read them in the
browser, with no sign-in, because the repositories are public:

| What | Where | Value today |
|---|---|---|
| Organization id (`mookie-studio`) | https://api.github.com/repos/mookie-studio/entry-hub, the `id` inside `owner` | `356768393` |
| Repository id of `entry-hub` | the same page, the first `id` | `1423551095` |
| Repository id of `entry-hub-frontend` | https://api.github.com/repos/mookie-studio/entry-hub-frontend, the first `id` | `1458145927` |

A credential built from numeric ids can use a different subject format from the name-based one above. What matters is
that the subject **equals the `sub` claim of the token that `azure/login` sends**, and by default GitHub issues the
name-based `repo:mookie-studio/entry-hub:environment:production`. If the form insists on ids and you have not
customised the subject claim in GitHub, create the credential with the scenario *Other issuer* instead: issuer
`https://token.actions.githubusercontent.com`, subject `repo:mookie-studio/entry-hub:environment:production`,
audience `api://AzureADTokenExchange`. If `azure/login` still fails with `AADSTS700213` ("No matching federated
identity record found for presented assertion subject"), the error prints the exact subject GitHub sent: create the
credential with that text.

## GitHub: environment, variables and secret

In the GitHub web, on the repository `mookie-studio/entry-hub`:

1. *Settings → Environments → New environment* named `production`. Tick *Required reviewers* and add the people who
   must approve a deployment, and under *Deployment branches and tags* choose *Selected branches and tags* and add a
   rule of type *Tag* with the pattern `v*`. (Required reviewers may not be available on a private repository of a free
   account.)
2. Create the five **repository variables** in *Settings → Secrets and variables → Actions → Variables tab → New
   repository variable* (fill in *Name* and *Value*, then *Add variable*). They are not environment variables.
3. Create the **secret** in *Settings → Environments → production → Environment secrets → Add environment secret*.

For each value, where to copy it in the Azure portal and where it goes in GitHub:

- `AZURE_CLIENT_ID`: copy *Microsoft Entra ID → App registrations → `entryhub-github` → Overview → Application
  (client) ID*. Paste it as a repository variable. It is not a secret.
- `AZURE_TENANT_ID`: copy *Directory (tenant) ID* from the same *Overview* page. Paste it as a repository variable.
- `AZURE_SUBSCRIPTION_ID`: copy *Subscriptions →* your subscription *→ Overview → Subscription ID*. Paste it as a
  repository variable.
- `AZURE_WEBAPP_NAME`: copy the name of the App Service (*App Services →* your app, the title of the page). Paste it as
  a repository variable. While it is empty, nothing is deployed.
- `API_URL`: copy *App Services →* your app *→ Overview → Default domain* and paste it as `https://<default-domain>`
  with no trailing slash, as a repository variable.
- `MIGRATIONS_CONNECTION_STRING`: the Npgsql connection string of the **owner** role of the database (`postgres.<ref>`
  on the Supabase session pooler, see [supabase-setup.md](supabase-setup.md)). Paste it as an **environment secret** of
  `production`, not as a variable and not as a repository secret.

The API itself gets its settings in the App Service, not from GitHub: in the portal, *Settings → Environment variables
→ App settings* (*Advanced edit* takes a JSON list of `name` and `value`), or `az webapp config appsettings set`:
`ConnectionStrings__EntryHub` with the **application** role, the `Authentication__*` settings of Supabase Auth, and
`Cors__AllowedOrigins__0` with the URL of the front end. Rate limiting and demo data have their own settings
(`RateLimiting__*`, `Demo__Seed`); see the README. After changing a setting, restart the app (*Overview → Restart*).

## Keeping Supabase awake

The free Supabase plan pauses a project after a week without activity. The `keep-alive` workflow calls
`GET /api/v1/health` every day at about 06:17 UTC; the call asks PostgreSQL, which counts as activity, and it also
wakes the App Service, which sleeps without Always On. It does nothing while `API_URL` is empty. To try it now:
*Actions → keep-alive → Run workflow*.
