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

## Tests

```bash
cd src
dotnet test
```

Each slice is tested in three layers: pure logic (no I/O), the handler against PostgreSQL, and the endpoint through
`WebApplicationFactory`. The tests apply the migrations themselves before they touch the database.

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
