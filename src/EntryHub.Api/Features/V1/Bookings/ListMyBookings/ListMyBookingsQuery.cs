namespace EntryHub.Api.Features.V1.Bookings.ListMyBookings;

/// <param name="IncludeCancelled">Also return cancelled bookings; they are hidden by default.</param>
/// <param name="Page">One-based page number; defaults to 1.</param>
/// <param name="PageSize">Items per page; defaults to 20 and is capped at 100.</param>
public sealed record ListMyBookingsQuery(bool? IncludeCancelled, int? Page, int? PageSize);