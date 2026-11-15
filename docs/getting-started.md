# Getting started with the EntryHub API

This guide takes you from a token to your first booking. Every call below works against a local API
(`http://localhost:5178`) and against a deployed one; only `API` changes. Starting the API alone, a second tenant and
every endpoint are in [local-development.md](local-development.md). The full contract is
[`contracts/EntryHub.Api.json`](../contracts/EntryHub.Api.json), also served as OpenAPI and a Scalar page by the API.

## 1. Get a token

Every endpoint except `GET /api/v1/health` needs `Authorization: Bearer <token>`. The token carries two things the API
trusts and nothing else:

| Claim | Meaning |
|---|---|
| `tenant_id` | The customer (tenant) the caller belongs to. A token without a valid one gets 403. |
| `user_role` | `Employee` or `Facilities` (the claim name is configurable, `Authentication:RoleClaim`; it defaults to `role`). |

With Supabase Auth, sign in with the email and password of a user and read `access_token` from the response. The
Custom Access Token Hook of the project adds both claims.

```bash
SUPABASE_URL=https://<project-ref>.supabase.co
SUPABASE_ANON_KEY=<publishable anon key>

TOKEN=$(curl -s "$SUPABASE_URL/auth/v1/token?grant_type=password" \
  -H "apikey: $SUPABASE_ANON_KEY" -H "Content-Type: application/json" \
  -d '{"email":"ana@acme.example","password":"<password>"}' | jq -r .access_token)
```

For local development the .NET SDK can sign tokens instead (see the README, "Get a token").

```bash
API=http://localhost:5178/api/v1
H="Authorization: Bearer $TOKEN"
curl -H "$H" $API/me
```

`/me` answers with your user id, role, tenant id and tenant name, so it is the quickest way to check a token.

## 2. Look around

The demo data has two tenants (Acme and Globex). Acme has two sites ("Madrid HQ" and "London office") and eight rooms.

```bash
curl -H "$H" $API/sites
curl -H "$H" $API/rules
curl -H "$H" $API/features
curl -H "$H" "$API/rooms?minCapacity=6&features=projector"
```

## 3. Find a free room and book it

```bash
curl -H "$H" "$API/rooms/availability?from=2026-12-01T09:00:00Z&to=2026-12-01T10:00:00Z&attendees=4"

curl -X POST -H "$H" -H "Content-Type: application/json" $API/bookings -d '{
  "roomId": "<room id>",
  "title": "Planning",
  "startAt": "2026-12-01T09:00:00Z",
  "endAt": "2026-12-01T10:00:00Z",
  "attendees": 4
}'
```

Times are UTC (`Z`); the opening hours are evaluated in the time zone of the room's site.

Move or cancel it later:

```bash
curl -X POST -H "$H" -H "Content-Type: application/json" $API/bookings/<booking id>/reschedule \
  -d '{"startAt":"2026-12-01T11:00:00Z","endAt":"2026-12-01T12:00:00Z"}'
curl -X POST -H "$H" $API/bookings/<booking id>/cancel
curl -H "$H" "$API/bookings/mine?includeCancelled=true"
```

A reschedule keeps the booking id and may add a `roomId` to move it to another room.

## 4. Manage the space (Facilities)

```bash
curl -X POST -H "$H" -H "Content-Type: application/json" $API/features -d '{"name":"Whiteboard","kind":"Equipment"}'
curl -X POST -H "$H" -H "Content-Type: application/json" $API/rooms \
  -d '{"name":"Atlas","floor":2,"capacity":8,"siteId":"<site id>","featureIds":["<feature id>"]}'
curl -X PUT  -H "$H" -H "Content-Type: application/json" $API/rules -d '{ ...rules... }'
curl -X PATCH -H "$H" -H "Content-Type: application/json" $API/rooms/<room id> -d '{"isActive":false}'
curl -H "$H" "$API/bookings?roomId=<room id>&status=Active"
```

Read the exact bodies in the OpenAPI document; the README table lists every route and who may call it.

## 5. Read the answers

- **Paging.** Lists answer `{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 57 }`; ask with `page` and `pageSize`.
- **Errors** are `application/problem+json` ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)).
  - `400`: the body has the wrong shape; the response lists the fields.
  - `401` / `403`: no valid token, wrong role, or someone else's booking.
  - `404`: it does not exist, or it belongs to another tenant.
  - `409`: the room is already booked in that slot, or a name is taken.
  - `422`: a booking rule failed. The body has a `violations` array, each with a stable `code`, for example
    `TooLateToReschedule` or `NotActive`.
  - `429`: rate limit. Wait the seconds in the `Retry-After` header.
  - `503`: the database did not answer; `Retry-After: 5`.
- **Live occupancy.** `GET /rooms/{id}/occupancy` is Server-Sent Events: `curl -N -H "$H" $API/rooms/<id>/occupancy`.
