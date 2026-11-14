namespace EntryHub.Api.Features.V1.Rooms.GetRoomSchedule;

/// <summary>
/// One busy range of a room. Everyone sees when it is taken; only Facilities and the organizer see what it is.
/// </summary>
/// <param name="BookingId">The booking; <see langword="null"/> when the caller may not see it.</param>
/// <param name="StartAt">Start of the booking.</param>
/// <param name="EndAt">End of the booking.</param>
/// <param name="IsMine">The caller organized this booking.</param>
/// <param name="Title">Title of the booking; <see langword="null"/> when the caller may not see it.</param>
/// <param name="OrganizerId">Organizer of the booking; <see langword="null"/> when the caller may not see it.</param>
public sealed record RoomScheduleEntry(Guid? BookingId, DateTimeOffset StartAt, DateTimeOffset EndAt, bool IsMine, string? Title, string? OrganizerId);