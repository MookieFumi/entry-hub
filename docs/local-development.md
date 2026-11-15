# Local development: run the API and call it with authentication

Every endpoint except `GET /api/v1/health` needs a Bearer token with two things in it: the **application role**
(`Employee` or `Facilities`) and a **`tenant_id`** (a GUID of an existing tenant). Without a valid `tenant_id` the API
answers 403. This guide shows three ways to get such a token and then calls the API.

The migrations create one tenant for you, `00000000-0000-0000-0000-000000000001`. Use it for every example.

## 1. Start the API

**Option A: the whole application (needs Docker or Podman).**

```bash
cd src
dotnet run --project EntryHub.AppHost
```

The AppHost starts PostgreSQL and the API (http://localhost:5178) and applies the migrations in Development.

**Option B: only the API, over a PostgreSQL you already have.**

```bash
cd src/EntryHub.Api
ConnectionStrings__EntryHub="Host=localhost;Port=5432;Database=entryhub;Username=entryhub;Password=entryhub" \
  dotnet run
```

Use an ordinary role (not a superuser) if you want Row-Level Security to protect the tenants, as it does in production.

Check it: `curl http://localhost:5178/api/v1/health`. With the AppHost, the API reference is the `scalar-api-reference`
resource in the Aspire dashboard.

## 2. Get a token

### 2a. `dotnet user-jwts` (simplest, no external service)

The .NET SDK signs tokens with a key kept in your user secrets, so nothing secret is in the repository. It works only
while the API has **no** `Authority` configured (see 2b).

```bash
cd src/EntryHub.Api
TENANT=00000000-0000-0000-0000-000000000001
dotnet user-jwts create --role Employee   --name ana  --claim tenant_id=$TENANT   # prints the token
dotnet user-jwts create --role Facilities --name fran --claim tenant_id=$TENANT
dotnet user-jwts list                  # ids and expiry
dotnet user-jwts print <id> --show-all # see the claims
```

Keep the token in a variable. Tokens last 3 months by default; `--valid-for 8h` shortens that.

```bash
ANA=<token of ana>
FRAN=<token of fran>
```

To try a second customer, insert another tenant and mint tokens with its id. Tokens of one tenant never see the data
of the other:

```bash
psql "$CONN" -c "insert into tenants (\"Id\",\"Name\",\"CreatedAt\") values (gen_random_uuid(),'Other', now()) returning \"Id\""
```

### 2b. Supabase Auth (the same path as the demo in the cloud)

Use it when you want to test the real provider. You need the Supabase project and its Custom Access Token Hook from
[supabase-setup.md](supabase-setup.md) (it adds `tenant_id` and `user_role` to each token). Start the API with:

```bash
export Authentication__Schemes__Bearer__Authority="https://<project-ref>.supabase.co/auth/v1"
export Authentication__Schemes__Bearer__ValidAudiences__0="authenticated"
export Authentication__RoleClaim="user_role"
```

With an `Authority` set, tokens from `dotnet user-jwts` are rejected on purpose. Sign in with the password grant; the
`apikey` is the project's publishable key:

```bash
TOKEN=$(curl -s "https://<project-ref>.supabase.co/auth/v1/token?grant_type=password" \
  -H "apikey: <publishable-key>" -H "Content-Type: application/json" \
  -d '{"email":"ana@example.com","password":"<password>"}' | jq -r .access_token)
```

Check the claims at https://jwt.io (never paste a production token): you need `sub`, `tenant_id` and `user_role`.
`role` will say `authenticated` and grants nothing. Supabase access tokens last 1 hour by default.

If the discovery document is not at `<authority>/.well-known/openid-configuration`, set
`Authentication__Schemes__Bearer__MetadataAddress`.

### 2c. In automated tests

`EntryHub.Api.Tests` signs its own tokens (`TestTokens`) and has a fake OIDC provider shaped like Supabase
(`FakeOidcProvider`), so tests need neither of the above. See the README for `ENTRYHUB_TEST_CONNECTION`.

## 3. Call the endpoints

```bash
API=http://localhost:5178/api/v1
H_ANA="Authorization: Bearer $ANA"; H_FRAN="Authorization: Bearer $FRAN"

curl -H "$H_ANA" $API/me                      # who am I: user, role, tenant
curl -H "$H_ANA" $API/rules                   # the booking rules the server enforces

# Facilities create a room; Employees get 403
ROOM=$(curl -s -X POST $API/rooms -H "$H_FRAN" -H "Content-Type: application/json" \
  -d '{"name":"Aula 1","floor":1,"capacity":8,"equipment":["Projector"]}' | jq -r .id)

# Rooms free for a slot (check /rules: weekdays, office hours, 15-minute steps, at least 15 minutes from now)
curl -H "$H_ANA" "$API/rooms/availability?from=2026-11-17T09:00:00%2B01:00&to=2026-11-17T10:00:00%2B01:00&attendees=4"

# Book it
BOOKING=$(curl -s -X POST $API/bookings -H "$H_ANA" -H "Content-Type: application/json" \
  -d "{\"roomId\":\"$ROOM\",\"title\":\"Planning\",\"startAt\":\"2026-11-17T09:00:00+01:00\",\"endAt\":\"2026-11-17T10:00:00+01:00\",\"attendees\":4}" | jq -r .id)

curl -H "$H_ANA"  $API/bookings/mine
curl -H "$H_FRAN" "$API/bookings?roomId=$ROOM"            # every booking, Facilities only
curl -H "$H_ANA"  "$API/rooms/$ROOM/schedule?from=2026-11-17T00:00:00Z&to=2026-11-18T00:00:00Z"
curl -X PATCH $API/rooms/$ROOM -H "$H_FRAN" -H "Content-Type: application/json" -d '{"isActive":false}'
curl -X POST $API/bookings/$BOOKING/reschedule -H "$H_ANA" -H "Content-Type: application/json" \
  -d '{"startAt":"2026-11-18T09:00:00+01:00","endAt":"2026-11-18T10:00:00+01:00"}'   # same id, new time
curl -X POST $API/bookings/$BOOKING/cancel -H "$H_ANA"
```

Dates need an offset (`Z` or `+01:00`); in a URL write `+` as `%2B`.

## 4. Call it from the browser front end

Run the React app on `http://localhost:5173`: Development already allows that origin (see *CORS* in the README).
Another origin: `export Cors__AllowedOrigins__0=http://localhost:3000`. The front end sends the token as
`Authorization: Bearer ...`; it never uses cookies.

## Troubleshooting

| You see | Cause |
|---|---|
| 401 | No token, expired, wrong signing key, or an `Authority` is set and the token comes from `user-jwts` (or the other way round) |
| 403 on everything | The token has no valid `tenant_id`, or its role claim is not the one in `Authentication:RoleClaim` |
| 403 on one endpoint | The role is not enough (for example an Employee creating a room) |
| 404 for data you just created | The token belongs to another tenant |
| 422 creating a booking | A booking rule; the body says which (`GET /rules` lists the rules of the tenant and `GET /sites` the hours of each site) |
| CORS error in the browser | The page origin is not in `Cors:AllowedOrigins` |
