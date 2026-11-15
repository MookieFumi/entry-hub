using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Rooms;
using EntryHub.Api.Tests.Support;
using EntryHub.Reminders;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace EntryHub.Api.Tests.Reminders;

/// <summary>
/// The release of bookings nobody checked in to runs against the real database. Each test makes its own tenant, because
/// the limit is a rule of the tenant and the shared test tenants must not get one.
/// </summary>
[Trait("Category", "Integration")]
public class AutoReleaseTests
{
    private static DateTimeOffset At(int minutes) => new DateTimeOffset(2036, 3, 10, 9, 0, 0, TimeSpan.Zero).AddMinutes(minutes);

    private static ReminderStore NewStore() => new(NpgsqlDataSource.Create(TestDatabase.ConnectionString));

    private static async Task<(Guid Tenant, Guid Room)> NewTenantAsync(int autoReleaseMinutes)
    {
        var (tenant, site) = await TestDatabase.CreateTenantWithSiteAsync($"Release {Guid.NewGuid():N}");
        await using var db = TestDatabase.CreateContext(tenantId: tenant);
        var room = new Room { Id = Guid.CreateVersion7(), SiteId = site, Name = "Room", Floor = 1, Capacity = 8, CreatedAt = At(-1000) };
        db.Rooms.Add(room);

        if (autoReleaseMinutes > 0)
        {
            db.TenantRules.Add(new TenantRules
            {
                MinDurationMinutes = 15,
                MaxDurationMinutes = 240,
                SlotMinutes = 15,
                MinLeadTimeMinutes = 15,
                MaxAdvanceDays = 90,
                CancellationNoticeMinutes = 15,
                MaxActiveBookingsPerEmployee = 5,
                AutoReleaseMinutes = autoReleaseMinutes,
                UpdatedAt = At(-1000),
            });
        }

        await db.SaveChangesAsync();
        return (tenant, room.Id);
    }

    private static async Task<Booking> NewBookingAsync(Guid tenant, Guid room, int startMinutes, int lengthMinutes = 60, bool checkedIn = false, BookingStatus status = BookingStatus.Confirmed)
    {
        await using var db = TestDatabase.CreateContext(tenantId: tenant);
        var booking = new Booking
        {
            Id = Guid.CreateVersion7(),
            RoomId = room,
            OrganizerId = $"person-{Guid.NewGuid():N}",
            Title = "Planning",
            StartAt = At(startMinutes),
            EndAt = At(startMinutes + lengthMinutes),
            Attendees = 4,
            Status = status,
            CheckedInAt = checkedIn ? At(startMinutes) : null,
            CreatedAt = At(-1000),
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking;
    }

    [Fact]
    public async Task A_booking_nobody_checked_in_to_is_released_once_the_limit_has_passed()
    {
        var (tenant, room) = await NewTenantAsync(autoReleaseMinutes: 15);
        var waiting = await NewBookingAsync(tenant, room, startMinutes: 0);

        Assert.Empty(await NewStore().ReleaseUncheckedAsync(At(14), CancellationToken.None));
        var released = await NewStore().ReleaseUncheckedAsync(At(15), CancellationToken.None);

        Assert.Equal([waiting.Id], released);
        await using var db = TestDatabase.CreateContext(tenantId: tenant);
        var booking = await db.Bookings.SingleAsync();
        Assert.Equal((BookingStatus.Cancelled, At(15)), (booking.Status, booking.CancelledAt));
        var audit = await db.AuditEntries.SingleAsync();
        Assert.Equal(("system", "Cancelled", "Booking", waiting.Id), (audit.ActorId, audit.Action, audit.EntityType, audit.EntityId));
        Assert.Empty(await NewStore().ReleaseUncheckedAsync(At(30), CancellationToken.None));
    }

    [Fact]
    public async Task Checked_in_cancelled_and_finished_bookings_are_never_released()
    {
        var (tenant, room) = await NewTenantAsync(autoReleaseMinutes: 10);
        await NewBookingAsync(tenant, room, startMinutes: 0, checkedIn: true);
        await NewBookingAsync(tenant, room, startMinutes: 120, status: BookingStatus.Cancelled);
        await NewBookingAsync(tenant, room, startMinutes: 240, lengthMinutes: 30);

        var released = await NewStore().ReleaseUncheckedAsync(At(400), CancellationToken.None);

        Assert.Empty(released);
    }

    [Fact]
    public async Task A_tenant_without_a_limit_keeps_all_its_bookings_and_one_tenant_does_not_affect_another()
    {
        var (withoutLimit, roomWithout) = await NewTenantAsync(autoReleaseMinutes: 0);
        var (withLimit, roomWith) = await NewTenantAsync(autoReleaseMinutes: 5);
        var kept = await NewBookingAsync(withoutLimit, roomWithout, startMinutes: 0);
        var released = await NewBookingAsync(withLimit, roomWith, startMinutes: 0);

        var result = await NewStore().ReleaseUncheckedAsync(At(30), CancellationToken.None);

        Assert.Equal([released.Id], result);
        await using var db = TestDatabase.CreateContext(tenantId: withoutLimit);
        Assert.Equal(BookingStatus.Confirmed, (await db.Bookings.SingleAsync(booking => booking.Id == kept.Id)).Status);
    }
}