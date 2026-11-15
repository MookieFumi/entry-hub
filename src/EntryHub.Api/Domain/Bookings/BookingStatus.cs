namespace EntryHub.Api.Domain.Bookings;

public enum BookingStatus
{
    Confirmed,
    Cancelled,

    /// <summary>
    /// Waiting for Facilities to approve or reject it; it holds the room meanwhile.
    /// </summary>
    Pending,
}