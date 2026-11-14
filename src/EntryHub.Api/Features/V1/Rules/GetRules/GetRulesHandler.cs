using System.Globalization;

using EntryHub.Api.Domain.Bookings;
using EntryHub.Api.Features.V1.Common;

using Microsoft.Extensions.Options;

namespace EntryHub.Api.Features.V1.Rules.GetRules;

public sealed class GetRulesHandler(IOptions<BookingRulesOptions> options)
{
    private static readonly DayOfWeek[] OfficeDays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    /// <summary>
    /// Today the rules are the same for every tenant. They move to the tenant when rules are stored per customer.
    /// </summary>
    public RulesResponse Handle(Caller caller)
    {
        var rules = options.Value;

        return new RulesResponse(
            (int)rules.MinDuration.TotalMinutes,
            (int)rules.MaxDuration.TotalMinutes,
            (int)rules.Slot.TotalMinutes,
            rules.OfficeOpens.ToString("HH\\:mm", CultureInfo.InvariantCulture),
            rules.OfficeCloses.ToString("HH\\:mm", CultureInfo.InvariantCulture),
            OfficeDays,
            rules.OfficeTimeZoneId,
            (int)rules.MinLeadTime.TotalMinutes,
            (int)rules.MaxAdvance.TotalDays,
            (int)rules.CancellationNotice.TotalMinutes,
            caller.IsFacilities ? null : rules.MaxActiveBookingsPerEmployee);
    }
}