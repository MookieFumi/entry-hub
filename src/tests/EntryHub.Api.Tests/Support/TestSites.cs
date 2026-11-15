namespace EntryHub.Api.Tests.Support;

/// <summary>
/// The site every test room belongs to, one per shared test tenant: a room cannot exist without a site.
/// </summary>
internal static class TestSites
{
    public static readonly Guid Default = new("a0000000-0000-0000-0000-0000000000a5");

    public static readonly Guid Other = new("b0000000-0000-0000-0000-0000000000b5");

    public static Guid For(Guid tenantId) => tenantId == TestTenants.Other ? Other : Default;
}