using System.Security.Claims;

using EntryHub.Api.Domain.Bookings;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace EntryHub.Api.Security;

/// <summary>
/// Object-level authorization for bookings: only the organizer and Facilities may read or cancel one.
/// </summary>
/// <remarks>
/// The endpoint policy (<see cref="Policies.AnyEmployee"/>) runs before the booking is loaded, so it can only say
/// who may call the endpoint. This handler runs once the booking is in memory and answers whether the caller may
/// act on that specific booking. Any operation it does not know is denied.
/// </remarks>
public sealed class BookingAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, Booking>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        Booking resource)
    {
        if (requirement.Name != BookingOperations.Read.Name && requirement.Name != BookingOperations.Cancel.Name)
        {
            return Task.CompletedTask;
        }

        var isOrganizer = context.User.FindFirstValue("sub") == resource.OrganizerId;

        if (isOrganizer || context.User.IsInRole(Roles.Facilities))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}