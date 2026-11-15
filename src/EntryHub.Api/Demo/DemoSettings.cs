namespace EntryHub.Api.Demo;

/// <summary>
/// The <c>Demo</c> configuration section.
/// </summary>
public sealed class DemoSettings
{
    /// <summary>
    /// Creates two demo tenants with rooms and a few bookings when the API starts. Off by default: nothing is seeded
    /// unless someone asks for it, and what is already there is never changed.
    /// </summary>
    public bool Seed { get; set; }
}