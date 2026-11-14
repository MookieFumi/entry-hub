namespace EntryHub.Api.Cors;

/// <summary>
/// The <c>Cors</c> configuration section.
/// </summary>
public sealed class CorsSettings
{
    /// <summary>
    /// Origins of the browser apps that may call the API, such as <c>https://entryhub-web.pages.dev</c>. Empty by
    /// default, which allows none. A subdomain wildcard is accepted (<c>https://*.entryhub-web.pages.dev</c> for the
    /// preview deployments of Cloudflare Pages); a lone <c>*</c> is refused.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];
}