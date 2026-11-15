using System.Security.Claims;

namespace EntryHub.Api.Tenancy;

/// <summary>
/// Who is making the change, for the audit trail.
/// </summary>
public interface ICurrentActor
{
    /// <summary>
    /// <see langword="null"/> when nobody is: code that runs outside a request (the demo seeder, tools) is not audited.
    /// </summary>
    string? ActorId { get; }
}

/// <summary>
/// Takes the actor from the <c>sub</c> claim of the request.
/// </summary>
public sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public string? ActorId => accessor.HttpContext?.User.FindFirstValue("sub");
}