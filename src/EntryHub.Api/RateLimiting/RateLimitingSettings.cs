namespace EntryHub.Api.RateLimiting;

/// <summary>
/// The <c>RateLimiting</c> configuration section. The defaults are small on purpose: the demo runs on App Service F1,
/// which has little CPU and no room for one noisy client.
/// </summary>
public sealed class RateLimitingSettings
{
    /// <summary>
    /// Turns the limiter off when <see langword="false"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Requests per window for each tenant, shared by all its users. A caller whose token has no tenant is counted on
    /// their own.
    /// </summary>
    public int PermitLimit { get; set; } = 120;

    /// <summary>
    /// Requests per window for each IP address that sends no token.
    /// </summary>
    public int AnonymousPermitLimit { get; set; } = 30;

    /// <summary>
    /// Length of the window, in seconds.
    /// </summary>
    public int WindowSeconds { get; set; } = 60;
}