using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace EntryHub.Api.Security;

/// <summary>
/// What someone may want to do with one booking. <see cref="BookingAuthorizationHandler"/> decides who may do it.
/// </summary>
public static class BookingOperations
{
    public static readonly OperationAuthorizationRequirement Read = new() { Name = nameof(Read) };

    public static readonly OperationAuthorizationRequirement Cancel = new() { Name = nameof(Cancel) };
}