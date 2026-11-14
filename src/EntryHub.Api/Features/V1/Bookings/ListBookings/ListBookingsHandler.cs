using EntryHub.Api.Data;
using EntryHub.Api.Features.V1.Common;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Bookings.ListBookings;

public sealed class ListBookingsHandler(EntryHubDbContext db)
{
    public async Task<PagedResponse<BookingResponse>> HandleAsync(ListBookingsQuery query, CancellationToken cancellationToken)
    {
        var paging = Paging.Normalize(query.Page, query.PageSize);

        var bookings = db.Bookings.AsNoTracking();

        if (query.RoomId is { } roomId)
        {
            bookings = bookings.Where(booking => booking.RoomId == roomId);
        }

        if (query.From is { } from)
        {
            bookings = bookings.Where(booking => booking.EndAt > from);
        }

        if (query.To is { } to)
        {
            bookings = bookings.Where(booking => booking.StartAt < to);
        }

        if (query.Status is { } status)
        {
            bookings = bookings.Where(booking => booking.Status == status);
        }

        var total = await bookings.CountAsync(cancellationToken);

        var page = await bookings
            .OrderBy(booking => booking.StartAt).ThenBy(booking => booking.Id)
            .Skip(paging.Skip).Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<BookingResponse>(page.Select(BookingResponse.From).ToList(), paging.Page, paging.PageSize, total);
    }
}