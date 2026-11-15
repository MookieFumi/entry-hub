# Release notes

## v1.1.0 (in progress)

- `POST /bookings` accepts an optional `Idempotency-Key` header. Retrying with the same key and body returns the original
  booking (with `Idempotent-Replayed: true`); the same key with a different body returns 422. Keys are private to each
  user and are remembered for 24 hours.

## v1.0.0

The first complete version: a multi-tenant room-booking API, with a React front end in its own repository.

### What it does

- **Tenants.** Every room and booking belongs to a tenant, and the tenant comes only from the `tenant_id` claim of the
  token. EF Core filters and PostgreSQL Row-Level Security both enforce it.
- **Sites and rules per tenant.** A tenant has sites (time zone, opening hours and days) and booking rules (duration,
  notice, active bookings per person) that Facilities edit through `/sites` and `/rules`.
- **Equipment catalog.** Equipment and accessibility entries per tenant (`/features`); rooms reference them and can be
  filtered by them. `equipment` stays in the room response as a list of names.
- **Bookings.** Create, list, cancel and now **reschedule** (`POST /bookings/{id}/reschedule`), which keeps the booking
  id and records `rescheduledAt`. Overlaps are refused by an exclusion constraint (409).
- **Availability and live occupancy** per room, with Server-Sent Events.
- **Authentication** with any OpenID Connect provider; the demo uses Supabase Auth.
- **Rate limiting**, RFC 9457 errors, a committed OpenAPI contract with a breaking-change check, and demo data for two
  tenants.

### Changes to know about when upgrading from the pre-release

- The tenant must be in the token (`tenant_id`); tokens without it get 403.
- The API must connect to PostgreSQL as an ordinary role without `BYPASSRLS`, or the second isolation barrier is off.
- Migrations are not applied at startup in production; run them as part of the deployment (see
  [deployment.md](deployment.md)).
- `rooms.Equipment` (free text) became the feature catalog; the migration converts existing text into catalog entries.
- Every room belongs to a site; the migration creates "Main office" for each existing tenant.

### Known limits

- The Azure deployment workflows and the Supabase integration are written but have not run against real accounts yet.
- Recurring bookings, approvals, check-in, webhooks and calendar integration are planned after v1.0.0.
