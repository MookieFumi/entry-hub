using System.Globalization;

using EntryHub.Api.Features.V1.Common;

namespace EntryHub.Api.Features.V1.Rules.GetRules;

public sealed class GetRulesHandler(TenantRulesReader reader)
{
    /// <summary>
    /// The rules of the tenant of the token. The office fields come from its default site, the oldest active one.
    /// </summary>
    public async Task<RulesResponse> HandleAsync(Caller caller, CancellationToken cancellationToken)
    {
        var rules = await reader.GetAsync(cancellationToken);
        var site = await reader.GetDefaultSiteHoursAsync(cancellationToken);

        return new RulesResponse(
            (int)rules.MinDuration.TotalMinutes,
            (int)rules.MaxDuration.TotalMinutes,
            (int)rules.Slot.TotalMinutes,
            site.Opens.ToString("HH\\:mm", CultureInfo.InvariantCulture),
            site.Closes.ToString("HH\\:mm", CultureInfo.InvariantCulture),
            site.OpenDays.Order().ToList(),
            site.TimeZoneId,
            (int)rules.MinLeadTime.TotalMinutes,
            (int)rules.MaxAdvance.TotalDays,
            (int)rules.CancellationNotice.TotalMinutes,
            caller.IsFacilities ? null : rules.MaxActiveBookingsPerEmployee);
    }
}