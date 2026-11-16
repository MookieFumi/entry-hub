# Reminders and notifications

The reminders service (`EntryHub.Reminders`) tells the organizer of a booking that it is about to start (15 minutes
before, see `Reminders:LeadTime`). How the reminder reaches them depends on its configuration.

## What is sent

By default a reminder is only a log entry: `Reminder for booking {id}, which starts at {time}`. It names neither the
booking title, the room nor a person, so the logs hold no personal data.

With SMTP turned on, each reminder is a plain text email to the organizer.

## Where the address comes from

The API keeps no user table: identity is the token. When an organizer books for themselves, the API copies the `email`
claim of their token into the booking (`OrganizerEmail`). Supabase Auth puts that claim in its tokens. A booking made
on behalf of someone else by Facilities, or with a token that has no `email`, has no address: its reminder is not an
error, it is counted in the log (`N without an address`) and considered done.

## Turn on email

SMTP is off until you set all of this for the reminders service (environment variables use `__` for the colon):

| Setting | Meaning | Default |
|---|---|---|
| `Notifications:Smtp:Enabled` | Turns email on. | `false` |
| `Notifications:Smtp:Host` | The mail server. | none |
| `Notifications:Smtp:Port` | Its port. | `587` |
| `Notifications:Smtp:UseSsl` | Encrypt the connection. Keep it on. | `true` |
| `Notifications:Smtp:UserName` / `Password` | Credentials, if the server needs them. | none |
| `Notifications:Smtp:From` | The sender address of every reminder. | none |
| `Notifications:Smtp:Timeout` | How long to wait for the server. | `00:00:15` |

Keep the password in a secret (user secrets locally, the environment's secret store when deployed), never in a file in
the repository. If `Enabled` is true but the host or the sender is missing, the service logs reminders as if SMTP were
off.

## Failures

A reminder is marked as sent only after the sender accepts it. If the mail server fails, the reminder is not marked,
the log says that a send failed and the type of the error (never the address), and the same reminder is offered again
after `Reminders:RetryAfter` (1 minute) for as long as the booking has not started.

## Webhooks

The same service delivers webhook events: see the release notes of v1.1.0 and the `Webhooks` settings
(`PollInterval`, `MaxAttempts`, `BaseDelay`, `Timeout`).
