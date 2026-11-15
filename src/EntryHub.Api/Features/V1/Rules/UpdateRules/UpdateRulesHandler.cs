using EntryHub.Api.Data;
using EntryHub.Api.Domain.Bookings;

using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Features.V1.Rules.UpdateRules;

public sealed class UpdateRulesHandler(EntryHubDbContext db, TimeProvider clock)
{
    /// <returns>The errors by field name, or <see langword="null"/> when the rules were saved.</returns>
    public async Task<Dictionary<string, string[]>?> HandleAsync(UpdateRulesRequest request, CancellationToken cancellationToken)
    {
        var errors = Validate(request);

        if (errors.Count > 0)
        {
            return errors;
        }

        var rules = await db.TenantRules.SingleOrDefaultAsync(cancellationToken);

        if (rules is null)
        {
            rules = new TenantRules();
            db.TenantRules.Add(rules);
        }

        rules.MinDurationMinutes = request.MinDurationMinutes;
        rules.MaxDurationMinutes = request.MaxDurationMinutes;
        rules.SlotMinutes = request.SlotMinutes;
        rules.MinLeadTimeMinutes = request.MinLeadTimeMinutes;
        rules.MaxAdvanceDays = request.MaxAdvanceDays;
        rules.CancellationNoticeMinutes = request.CancellationNoticeMinutes;
        rules.MaxActiveBookingsPerEmployee = request.MaxActiveBookings;
        rules.UpdatedAt = clock.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);

        return null;
    }

    private static Dictionary<string, string[]> Validate(UpdateRulesRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.MaxDurationMinutes < request.MinDurationMinutes)
        {
            errors["maxDurationMinutes"] = ["The longest booking cannot be shorter than the shortest."];
        }

        if (60 % request.SlotMinutes != 0)
        {
            errors["slotMinutes"] = ["The slot must divide an hour: 1, 2, 3, 4, 5, 6, 10, 12, 15, 20, 30 or 60 minutes."];
        }

        if (request.MinDurationMinutes % request.SlotMinutes != 0 || request.MaxDurationMinutes % request.SlotMinutes != 0)
        {
            errors["minDurationMinutes"] = ["The shortest and the longest booking must be multiples of the slot."];
        }

        return errors;
    }
}