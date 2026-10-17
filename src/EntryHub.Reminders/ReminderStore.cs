using Npgsql;

namespace EntryHub.Reminders;

/// <summary>
/// A booking whose organizer has just been marked as reminded.
/// </summary>
public sealed record DueReminder(Guid BookingId, string OrganizerId, string Title, string RoomName, DateTimeOffset StartAt);

/// <summary>
/// Reads the bookings of the API database that are about to start. The API owns the schema; this service only
/// reads <c>bookings</c> and <c>rooms</c> and writes <c>ReminderSentAt</c>.
/// </summary>
public sealed class ReminderStore(NpgsqlDataSource dataSource)
{
    // One statement finds the due bookings, locks them and marks them, so two copies of this service never pick the
    // same booking: the second one skips the rows the first one holds.
    private const string ClaimSql = """
        WITH due AS (
            SELECT "Id" FROM bookings
            WHERE "Status" = 'Confirmed'
              AND "ReminderSentAt" IS NULL
              AND "StartAt" > @now
              AND "StartAt" <= @until
            FOR UPDATE SKIP LOCKED)
        UPDATE bookings AS booking
        SET "ReminderSentAt" = @now
        FROM due, rooms AS room
        WHERE booking."Id" = due."Id" AND room."Id" = booking."RoomId"
        RETURNING booking."Id", booking."OrganizerId", booking."Title", room."Name", booking."StartAt"
        """;

    /// <summary>
    /// Marks the confirmed bookings that start within <paramref name="leadTime"/> as reminded and returns them.
    /// Each booking comes back once, so the reminder is delivered at most once.
    /// </summary>
    public async Task<IReadOnlyList<DueReminder>> ClaimDueAsync(DateTimeOffset now, TimeSpan leadTime, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(ClaimSql);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("until", now + leadTime);

        var due = new List<DueReminder>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            due.Add(new DueReminder(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4)));
        }

        return due;
    }
}