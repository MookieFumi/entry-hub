using System.Security.Claims;

using EntryHub.Api.Data;
using EntryHub.Api.Security;

using Microsoft.AspNetCore.Authorization;
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

public sealed class GetBookingHandler(EntryHubDbContext db, IAuthorizationService authorization)
{
    public async Task<GetBookingResult> HandleAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(booking => booking.Id == id, cancellationToken);

        if (booking is null)
        {
            return new GetBookingResult.NotFound();
        }

        var result = await authorization.AuthorizeAsync(user, booking, BookingOperations.Read);

        return result.Succeeded
            ? new GetBookingResult.Found(BookingResponse.From(booking))
            : new GetBookingResult.Forbidden();
    }
}