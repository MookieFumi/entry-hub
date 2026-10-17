using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Common;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Bookings.ListMyBookings;

public sealed class ListMyBookingsHandler(EntryHubDbContext db)
{
    public async Task<PagedResponse<BookingResponse>> HandleAsync(
        Caller caller,
        ListMyBookingsQuery query,
        CancellationToken cancellationToken)
    {
        var paging = Paging.Normalize(query.Page, query.PageSize);

        var bookings = db.Bookings.AsNoTracking().Where(booking => booking.OrganizerId == caller.Id);

        if (query.IncludeCancelled != true)
        {
            bookings = bookings.Where(booking => booking.Status == BookingStatus.Confirmed);
        }

        var total = await bookings.CountAsync(cancellationToken);

        var page = await bookings
            .OrderByDescending(booking => booking.StartAt)
            .Skip(paging.Skip).Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<BookingResponse>(page.Select(BookingResponse.From).ToList(), paging.Page, paging.PageSize, total);
    }
}