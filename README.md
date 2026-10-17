# entry-hub

<p>
  <a href="https://github.com/MookieFumi/entry-hub/actions/workflows/ci.yml"><img alt="CI" src="https://img.shields.io/github/actions/workflow/status/MookieFumi/entry-hub/ci.yml?branch=main&amp;label=CI&amp;logo=githubactions&amp;logoColor=white&amp;style=flat-square"></a>
  <a href="https://github.com/MookieFumi/entry-hub/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/MookieFumi/entry-hub?logo=github&amp;logoColor=white&amp;style=flat-square"></a>
  <a href="https://mookiefumi.github.io/entry-hub/"><img alt="API docs" src="https://img.shields.io/badge/API-docs-85EA2D?logo=swagger&amp;logoColor=black&amp;style=flat-square"></a>
  <a href="https://github.com/MookieFumi/entry-hub/commits/main"><img alt="Last commit" src="https://img.shields.io/github/last-commit/MookieFumi/entry-hub/main?logo=git&amp;logoColor=white&amp;style=flat-square"></a>
  <a href="https://github.com/MookieFumi/entry-hub/blob/main/LICENSE"><img alt="License: MIT" src="https://img.shields.io/github/license/MookieFumi/entry-hub?style=flat-square"></a>
  <a href="https://github.com/MookieFumi/entry-hub/pulls"><img alt="Open pull requests" src="https://img.shields.io/github/issues-pr/MookieFumi/entry-hub?logo=github&amp;logoColor=white&amp;style=flat-square"></a>
</p>
<p>
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&amp;logoColor=white&amp;style=flat-square">
  <img alt="ASP.NET Core Minimal APIs" src="https://img.shields.io/badge/ASP.NET%20Core-Minimal%20APIs-512BD4?logo=dotnet&amp;logoColor=white&amp;style=flat-square">
  <img alt="EF Core 10" src="https://img.shields.io/badge/EF%20Core-10-512BD4?logo=dotnet&amp;logoColor=white&amp;style=flat-square">
  <img alt="PostgreSQL 17" src="https://img.shields.io/badge/PostgreSQL-17-4169E1?logo=postgresql&amp;logoColor=white&amp;style=flat-square">
  <img alt="Docker Compose" src="https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&amp;logoColor=white&amp;style=flat-square">
</p>
<p>
  <a href="https://www.conventionalcommits.org"><img alt="Conventional Commits" src="https://img.shields.io/badge/Conventional%20Commits-1.0.0-FE5196?logo=conventionalcommits&amp;logoColor=white&amp;style=flat-square"></a>
  <a href="https://semver.org"><img alt="SemVer with GitVersion" src="https://img.shields.io/badge/SemVer-GitVersion-blue?logo=semver&amp;logoColor=white&amp;style=flat-square"></a>
  <a href="https://github.com/MookieFumi/entry-hub/blob/main/.github/dependabot.yml"><img alt="Dependabot" src="https://img.shields.io/badge/Dependabot-enabled-025E8C?logo=dependabot&amp;logoColor=white&amp;style=flat-square"></a>
</p>

Room booking API used as the demo project for the blog series about Minimal APIs, OpenAPI and Aspire.
Each post links to the code of the pull request that backs it.

The domain is room booking: rooms, bookings with business rules, JWT roles and a live occupancy stream.
It grows pull request by pull request, one feature per post.

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

## API v1

Everything except `health` needs a JWT Bearer token. There are two roles: `Employee` (books and cancels their own
bookings, reads rooms) and `Facilities` (also creates rooms and cancels any booking).

| Method and route | Slice | Who | Notes |
|---|---|---|---|
| `GET /api/v1/health` | `Health/GetHealth` | anyone | 200, or 503 when PostgreSQL does not answer |
| `POST /api/v1/rooms` | `Rooms/CreateRoom` | Facilities | 201, 400 on a bad body, 409 when the name is taken |
| `GET /api/v1/rooms/{id}` | `Rooms/GetRoom` | any role | 200 or 404 |
| `GET /api/v1/rooms` | `Rooms/ListRooms` | any role | filters `floor` and `minCapacity`, paging with `page` and `pageSize` |
| `GET /api/v1/rooms/{id}/occupancy` | `Rooms/StreamRoomOccupancy` | any role | Server-Sent Events; free or occupied every 5 seconds |
| `POST /api/v1/bookings` | `Bookings/CreateBooking` | any role | 201, 400 bad body, 422 broken rule, 409 slot taken |
| `GET /api/v1/bookings/{id}` | `Bookings/GetBooking` | owner or Facilities | 200, 403 or 404 |
| `GET /api/v1/bookings/mine` | `Bookings/ListMyBookings` | any role | paged; `includeCancelled=true` to see cancelled ones |
| `POST /api/v1/bookings/{id}/cancel` | `Bookings/CancelBooking` | owner or Facilities | 204; 422 inside the notice period for Employees |

### Errors

- **400**: the body has the wrong shape (validation built into .NET, `AddValidation`). The response lists the fields.
- **422**: the body is fine but a business rule fails. The `violations` array has a stable `code` per rule.
- **409**: the room is already booked. The database decides, through an exclusion constraint, not the C# code.

### Business rules

They live in `BookingRulesOptions` and can be changed in the `BookingRules` configuration section:
15 minutes to 4 hours in 15-minute steps, Monday to Friday from 08:00 to 20:00 in the office time zone
(`Europe/Madrid`), starting between 15 minutes and 90 days from now, cancellable until 15 minutes before the start
(Facilities at any time), and at most 5 active bookings per Employee (Facilities has no limit).

## Run it locally

Requirements: the .NET 10 SDK and Docker.

<!-- Docker Compose section: the Aspire AppHost replaces it, so this block and docker-compose.yml go away together. -->
```bash
docker compose up -d        # PostgreSQL on localhost:5432
cd src
dotnet run --project EntryHub.Api
curl http://localhost:5178/api/v1/health
```

### Get a token

Tokens for local development come from the .NET SDK. The signing key stays in your user secrets, so nothing secret
is in the repository.

```bash
cd src/EntryHub.Api
dotnet user-jwts create --role Employee --name ana        # prints the token
dotnet user-jwts create --role Facilities --name fran
curl -H "Authorization: Bearer <token>" http://localhost:5178/api/v1/rooms
```

In Development the API applies the EF Core migrations on startup. To add one after changing the model:

```bash
cd src
dotnet tool restore
dotnet dotnet-ef migrations add <Name> --project EntryHub.Api --output-dir Data/Migrations
```

The development connection string lives in `src/EntryHub.Api/appsettings.Development.json`
and matches `docker-compose.yml`. It is a throwaway local credential, not a secret.

## API documentation and contract

In Development the API serves an interactive page at `/scalar/v1` (Scalar, bundled with the app, no CDN) and the
OpenAPI 3.1 document at `/openapi/v1.json` and `/openapi/v1.yaml`. Authorize with a token from `dotnet user-jwts`
(see above) using the lock button.

Outside Development the interactive page does not exist and the document needs a token with the `Facilities` role,
so the contract is never public by accident. Open it to everyone on purpose by removing `RequireAuthorization` in
`Documentation/OpenApiExtensions.cs`.

The contract is also generated at build time into `contracts/EntryHub.Api.json` and committed. CI fails if an
endpoint changes and that file does not, so every API change shows up in the pull request diff.

## Releases

Push a tag such as `v1.0.0` and the `release` workflow creates a GitHub Release with the contract attached
(`openapi-v1.json`) and publishes the static docs of that version to GitHub Pages. Pages only ever shows the latest
release (https://mookiefumi.github.io/entry-hub/); older contracts stay attached to their releases. Enable it once in Settings > Pages, source GitHub Actions.

## Tests

```bash
cd src
dotnet test
```

Each slice is tested in three layers: pure logic (no I/O), the handler against PostgreSQL, and the endpoint through
`WebApplicationFactory`. The tests apply the migrations themselves before they touch the database.

Handler and endpoint tests need a PostgreSQL. By default they use the one from Docker Compose.
Set `ENTRYHUB_TEST_CONNECTION` to point them somewhere else (CI does this with a service container).

## What CI reports on each pull request

Every run of the `ci` workflow writes a report to its summary page, and on pull requests it keeps a single
comment up to date (it is edited on each push, never duplicated):

- **Version:** what GitVersion calculates for that branch, so a `feat` or a `fix` shows its effect before the merge.
- **Tests:** total, passed, failed and skipped, and the name of every failing test.
- **Coverage:** line and branch coverage of `EntryHub.Api` and a table per class. Generated EF Core migrations are
  left out (see `src/tests/coverage.runsettings`).

The coverage comes from `coverlet.collector` and the report from ReportGenerator, both open source and run inside the
workflow, so no external service or token is needed. A coverage badge would need one (Codecov or a gist); it is not
set up.

To get the same numbers locally:

```bash
cd src
dotnet test --settings tests/coverage.runsettings --collect:"XPlat Code Coverage" --results-directory ../TestResults
dotnet tool restore
dotnet reportgenerator "-reports:../TestResults/**/coverage.cobertura.xml" "-targetdir:../TestResults/report" "-reporttypes:Html"
```

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

## License

[MIT](LICENSE) © 2026 Miguel A. Martín Hrdez
