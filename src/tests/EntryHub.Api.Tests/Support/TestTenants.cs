namespace EntryHub.Api.Tests.Support;

/// <summary>
/// The two tenants of the integration tests. Most tests use <see cref="Default"/>; the isolation tests use both.
/// </summary>
internal static class TestTenants
{
    public static readonly Guid Default = new("a0000000-0000-0000-0000-00000000000a");

    public static readonly Guid Other = new("b0000000-0000-0000-0000-00000000000b");
}