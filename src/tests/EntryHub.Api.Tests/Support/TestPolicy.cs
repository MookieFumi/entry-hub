using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Domain.Sites;
using EntryHub.Api.Features.V1.Rules;

using Microsoft.Extensions.Options;

namespace EntryHub.Api.Tests.Support;

internal static class TestPolicy
{
    public static BookingPolicy Default() => new();

    /// <summary>
    /// A reader of the rules of the tenant of <paramref name="db"/>, with the configured defaults.
    /// </summary>
    public static TenantRulesReader Reader(EntryHubDbContext db) => new(db, Options.Create(new BookingRulesOptions()));

    /// <summary>
    /// The rules of a tenant that stored none.
    /// </summary>
    public static BookingRules Rules() => BookingRules.From(new BookingRulesOptions());

    /// <summary>
    /// A site in Madrid open Monday to Friday, 08:00 to 20:00, like the default one.
    /// </summary>
    public static SiteHours Madrid() => new("Europe/Madrid", new TimeOnly(8, 0), new TimeOnly(20, 0), Site.WorkingWeek);
}