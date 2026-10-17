using EntryHub.Api.Domain.Bookings;

using Microsoft.Extensions.Options;

namespace EntryHub.Api.Tests.Support;

internal static class TestPolicy
{
    public static BookingPolicy Default() => new(Options.Create(new BookingRulesOptions()));
}