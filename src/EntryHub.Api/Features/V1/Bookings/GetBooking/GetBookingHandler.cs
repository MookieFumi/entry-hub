using EntryHub.Api.Data;
using EntryHub.Api.Features.V1.Common;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Bookings.GetBooking;

public abstract record GetBookingResult
{
    private GetBookingResult()
    {
    }

    public sealed record Found(BookingResponse Booking) : GetBookingResult;

    public sealed record NotFound : GetBookingResult;

    public sealed record Forbidden : GetBookingResult;
}

public sealed class GetBookingHandler(EntryHubDbContext db)
{
    public async Task<GetBookingResult> HandleAsync(Caller caller, Guid id, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(booking => booking.Id == id, cancellationToken);

        if (booking is null)
        {
            return new GetBookingResult.NotFound();
        }

        return booking.OrganizerId == caller.Id || caller.IsFacilities
            ? new GetBookingResult.Found(BookingResponse.From(booking))
            : new GetBookingResult.Forbidden();
    }
}