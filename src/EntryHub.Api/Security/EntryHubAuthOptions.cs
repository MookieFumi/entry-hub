namespace EntryHub.Api.Security;

/// <summary>
/// Settings of the <c>Authentication</c> section that the framework does not know about.
/// </summary>
public sealed class EntryHubAuthOptions
{
    public const string RoleClaimDefault = "role";

    /// <summary>
    /// The claim that holds the application role (<c>Employee</c> or <c>Facilities</c>). It is <c>role</c> for tokens
    /// from <c>dotnet user-jwts</c>. Supabase Auth uses <c>role</c> for its own PostgreSQL role (<c>authenticated</c>),
    /// so there the application role travels in another claim, such as <c>user_role</c>.
    /// </summary>
    public string RoleClaim { get; set; } = RoleClaimDefault;
}