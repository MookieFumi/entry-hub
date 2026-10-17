# entry-hub

Room booking API used as the demo project for the blog series about Minimal APIs, OpenAPI and Aspire.
Each post links to the code of the pull request that backs it.

The domain (rooms and bookings) arrives feature by feature. This first version only contains the foundation:
a versioned API (`/api/v1`), PostgreSQL, tests and the governance rules.

## Layout

```text
src/
  EntryHub.slnx              solution (slnx format)
  Directory.Build.props      shared build properties and quality rules
  Directory.Packages.props   Central Package Management: one version per package
  EntryHub.Api/              Minimal API organized by vertical slices (Features/V1/...)
  tests/EntryHub.Api.Tests/  unit, handler and endpoint tests
.github/                     workflows, composite governance action, Dependabot, CODEOWNERS
.husky/                      Git hook that validates Conventional Commits
GitVersion.yml               semantic version derived from tags and history
```

Every feature follows the same slice shape: a request, a handler that holds the logic and an endpoint that only
delegates to the handler. See `src/EntryHub.Api/Features/V1/Health/GetHealth` for the smallest example.

## Run it locally

Requirements: the .NET 10 SDK and Docker.

<!-- Docker Compose section: the Aspire AppHost replaces it, so this block and docker-compose.yml go away together. -->
```bash
docker compose up -d        # PostgreSQL on localhost:5432
cd src
dotnet run --project EntryHub.Api
curl http://localhost:5178/api/v1/health
```

The development connection string lives in `src/EntryHub.Api/appsettings.Development.json`
and matches `docker-compose.yml`. It is a throwaway local credential, not a secret.

## Tests

```bash
cd src
dotnet test
```

Handler and endpoint tests need a PostgreSQL. By default they use the one from Docker Compose.
Set `ENTRYHUB_TEST_CONNECTION` to point them somewhere else (CI does this with a service container).

## Governance

| Rule | Where |
| --- | --- |
| Common properties and analyzers in one place | `src/Directory.Build.props`, `src/.editorconfig` |
| One version per package, locked restore | `src/Directory.Packages.props`, `packages.lock.json` files |
| Conventional Commits | `.husky/` (local hook), `.github/workflows/pr-title.yml` (PR title) |
| Semantic versioning | `GitVersion.yml`, `.config/dotnet-tools.json` |
| Build, format, vulnerabilities and tests on every PR | `.github/workflows/ci.yml`, `.github/actions/net-governance` |
| Review of governance files | `.github/CODEOWNERS` |
| Dependency updates | `.github/dependabot.yml` |

Commit messages and pull request titles use Conventional Commits and are written in English:
`type(scope): description`, for example `feat(rooms): add room endpoints`.
The hook is installed on the first `dotnet restore` in Debug. Set `HUSKY=0` to skip it.
