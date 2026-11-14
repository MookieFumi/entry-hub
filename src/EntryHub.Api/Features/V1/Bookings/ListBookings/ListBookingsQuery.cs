using EntryHub.Api.Domain.Bookings;

namespace EntryHub.Api.Features.V1.Bookings.ListBookings;

/// <param name="RoomId">Only bookings of this room.</param>
/// <param name="From">Only bookings that end after this moment.</param>
/// <param name="To">Only bookings that start before this moment.</param>
/// <param name="Status">Only bookings in this status; every status when omitted.</param>
/// <param name="Page">One-based page number; defaults to 1.</param>
/// <param name="PageSize">Items per page; defaults to 20 and is capped at 100.</param>
public sealed record ListBookingsQuery(Guid? RoomId, DateTimeOffset? From, DateTimeOffset? To, BookingStatus? Status, int? Page, int? PageSize);