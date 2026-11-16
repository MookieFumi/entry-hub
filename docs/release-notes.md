# Release notes

## v1.1.0 (in progress)

- **Email reminders.** The reminders service sends each reminder through an `INotificationSender`. The default only
  logs it, now without the title, the room or any person. Setting `Notifications:Smtp:*` (off by default, see
  `docs/notifications.md`) sends an email instead. The address comes from the optional `email` claim of the token of
  the organizer, kept in the booking when they book for themselves; a booking without one is counted in the log and
  never fails. A reminder is marked as sent only after the send worked: a failed one is offered again after a minute
  (`Reminders:RetryAfter`). Going live needs an SMTP provider and its credentials.
- **Webhook delivery.** A booking that is created, cancelled or moved saves an event for each enabled subscription that
  wants it, in the same transaction as the change. The reminders service sends it as a `POST` with a JSON body
  (`id`, `type`, `createdAt` and the booking in `data`) and these headers: `X-EntryHub-Event`, `X-EntryHub-Delivery`,
  `X-EntryHub-Timestamp` (Unix seconds) and `X-EntryHub-Signature`, which is `v1=` and the lowercase hex HMAC-SHA256 of
  `{timestamp}.{body}` with the secret of the subscription. Receivers should check the signature and ignore an old
  timestamp. Any 2xx answer is a success. Anything else (a redirect included, which is not followed) is tried again
  after 30 seconds and then 1, 2, 4 and 8 minutes, and given up after six attempts. Requests time out after 10 seconds
  and only connect to public addresses, checked on the address that is really used. Facilities read the outcome with
  `GET /webhooks/{id}/deliveries` (`status` filter, paged). Several copies of the service can run: each delivery is
  reserved before it is sent. Limits to know: it needs the reminders service to be running (the demo does not deploy
  it), the bookings that the service releases because nobody checked in do not send `booking.cancelled` yet, and the
  secret of a subscription is stored as text because it is needed to sign.
- **Webhook subscriptions.** Facilities manage them with `POST/GET /webhooks`, `PATCH /webhooks/{id}` (enable or
  disable) and `DELETE /webhooks/{id}`: an `https` URL and the events it wants (`booking.created`, `booking.cancelled`,
  `booking.rescheduled`). The response that creates a subscription shows its signing secret once; it is never listed
  again. URLs with another scheme, credentials, a local name or a loopback, private, link-local or shared address are
  rejected (400), a tenant can have 10 subscriptions (409) and every change is audited. This only stores the
  subscriptions: the delivery of the events comes next.
- **Cancel a series.** `POST /bookings/{id}/cancel?scope=series` cancels the booking and all the later occurrences of
  its series. The ones that the cancellation notice no longer allows (Facilities have no notice) are kept, and the
  response (200) lists the bookings that were cancelled and the ones that were kept, each with the rule that kept it.
  Without `scope` (or with `scope=occurrence`) only that booking is cancelled, as before (204). Each cancelled
  occurrence gets its own audit entry.
- **Recurring bookings.** `POST /bookings` accepts an optional `recurrence` (`frequency` Daily or Weekly, `interval`, and
  either `count` or `until`, at most 52 occurrences). The days are counted in the time zone of the site and the time of
  day is kept, so a 10:00 booking is still at 10:00 after the clocks change. Each occurrence goes through the same
  rules, holidays, blocks and overlap check as a single booking, and the ones that fail are skipped: the series is
  created when at least one fits (422 when none does). The 201 response is the first booking, with `seriesId`,
  `seriesPosition` and `series`, which lists every requested occurrence with its `bookingId` or the `violations` that
  skipped it. Repeating the request with the same `Idempotency-Key` returns the series that was created (a replay lists
  only the occurrences that were booked). Every occurrence counts toward the active-bookings limit of the organizer.
- **Release of bookings nobody checks in to.** A tenant can set `autoReleaseMinutes` in its rules (`PUT /rules`, 0 to
  240, off by default). The reminders service then cancels a confirmed booking that nobody checked in to within that
  many minutes after its start (while it has not ended), so the room is free again. The cancellation is audited with
  the actor `system`. Like the reminders, it needs the reminders service to be running, which the demo does not deploy.
- **Rooms that need approval.** A room can be marked `requiresApproval` (when it is created or edited). A booking of
  such a room by an Employee is created as `Pending` (201) and holds the room like a confirmed one: nobody else can
  book it and the availability search skips it. Bookings by Facilities are confirmed at once. A pending booking can be
  cancelled by its organizer.
- **Approve or reject.** Facilities decide a pending booking with `POST /bookings/{id}/approve` or
  `POST /bookings/{id}/reject` (the body with a `reason` is optional). Approving confirms it; rejecting cancels it and
  frees the room. The booking shows `decidedBy`, `decidedAt` and `decisionReason`. Anything but a pending booking gets
  409, and a booking that already ended cannot be approved. `GET /bookings?status=Pending` lists what waits.
- **Check-in.** The organizer or Facilities confirm that people showed up with `POST /bookings/{id}/check-in`, from 15
  minutes before the start until the end of the booking. The booking shows `checkedInAt`; checking in twice keeps the
  first moment. Too early gets a 422 with `TooEarlyToCheckIn`; a cancelled or finished booking gets `NotActive`.
- **Tenant data export.** Facilities download everything the tenant has stored (sites, holidays, equipment catalog,
  rooms, blocks, rules, bookings and the audit trail) as one JSON document with `GET /tenant/export`. Each export is
  recorded in the audit trail as `Exported`.
- **Tenant data deletion.** Facilities erase everything the tenant has stored with `DELETE /tenant/data` and a body
  `{ "confirmation": "DELETE <name of the tenant>" }` (bookings, rooms, sites, holidays, catalog, blocks, rules and the
  audit trail), all in one transaction. The tenant itself stays, together with one audit entry that says who deleted
  the data. Export first: it cannot be undone.
- **Room buffers.** A room can keep minutes free before and after each booking (`bufferBeforeMinutes` and
  `bufferAfterMinutes`, 0 to 240, set when the room is created or edited). A booking that would start or end inside the
  buffer of a neighbour gets the same 409 as an overlap, and the availability search leaves such rooms out. Each booking
  keeps the buffers the room had when it was made or moved, so editing a room does not change bookings that exist.
- **Audit trail.** Every change made through the API to bookings (created, rescheduled, cancelled), rooms, sites, rules,
  the equipment catalog, holidays and blocks is recorded with who, when and which fields changed (never their values),
  in the same transaction as the change. Facilities read it with `GET /audit`, newest first, filtered by entity type,
  entity, person or dates. Changes made outside a request, such as the demo seeder, are not recorded.
- **Book on behalf of someone else.** Facilities can pass `onBehalfOf` (the user's id) in `POST /bookings`; anyone else
  gets 403. The booking belongs to that user (it shows in their bookings and they can cancel or move it) and the new
  `createdBy` field of the booking says who made it. The active-bookings limit does not apply, as for any Facilities booking.
- **Maintenance blocks.** Facilities block a room for a time range (`POST /rooms/{id}/blocks`, also `GET` and `DELETE`).
  Bookings and reschedules that overlap a block get a 422 with the code `RoomBlocked`, and the availability search
  skips the room. Creating a block over confirmed bookings returns 409 with their ids in `bookingIds`.
- **Holidays per site.** Facilities close a site on a date with `POST /sites/{id}/holidays` (also `GET` and
  `DELETE`). Bookings and reschedules on that day get a 422 with the code `SiteClosed`, and the availability search
  skips the rooms of a closed site. Existing bookings are not touched.
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
