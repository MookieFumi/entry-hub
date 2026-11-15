using System.ComponentModel.DataAnnotations;

namespace EntryHub.Api.Features.V1.Bookings.DecideBooking;

/// <summary>
/// Optional body of a rejection.
/// </summary>
/// <param name="Reason">Why the booking is rejected, shown to whoever reads the booking.</param>
public sealed record RejectBookingRequest([property: StringLength(500)] string? Reason);