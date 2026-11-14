using EntryHub.Api.Tenancy;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace EntryHub.Api.Security;

public static class SecurityExtensions
{
    /// <summary>
    /// JWT Bearer authentication read from the <c>Authentication:Schemes:Bearer</c> section. Two ways to validate tokens:
    /// a signing key (what <c>dotnet user-jwts</c> writes, in user secrets) or an OpenID Connect provider, by setting
    /// <c>Authority</c> and <c>ValidAudiences</c>, which fetches the public keys (JWKS) from the provider. Supabase Auth
    /// is such a provider. <c>MetadataAddress</c> overrides the discovery document when the provider serves it elsewhere.
    /// <c>Authentication:RoleClaim</c> names the claim that holds the application role.
    /// </summary>
    public static IServiceCollection AddEntryHubSecurity(this IServiceCollection services)
    {
        services
            .AddAuthentication()
            .AddJwtBearer(options =>
            {
                // Keep the claim names of the token ("sub", "tenant_id", ...) instead of renaming them to long URIs.
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = "sub";

                // By default an expired token is still accepted for five more minutes (TokenValidationParameters
                // .DefaultClockSkew). Thirty seconds covers the clock drift between servers without that window.
                options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
            });

        // Read when the first token arrives, not when the application starts, so test hosts can set them last.
        services.AddOptions<EntryHubAuthOptions>().BindConfiguration("Authentication");
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<EntryHubAuthOptions>, IConfiguration>((jwt, auth, configuration) =>
            {
                var roleClaim = auth.Value.RoleClaim;
                jwt.TokenValidationParameters.RoleClaimType = string.IsNullOrWhiteSpace(roleClaim) ? EntryHubAuthOptions.RoleClaimDefault : roleClaim;

                if (configuration["Authentication:Schemes:Bearer:MetadataAddress"] is { Length: > 0 } metadataAddress)
                {
                    jwt.MetadataAddress = metadataAddress;
                }
            });

        // The tenant comes only from the signed token. A token without a valid tenant_id claim is refused (403)
        // on every protected endpoint, so no handler ever runs without a tenant.
        services
            .AddAuthorizationBuilder()
            .AddPolicy(Policies.AnyEmployee, policy => policy
                .RequireAssertion(context => TenantClaims.Read(context.User) is not null)
                .RequireRole(Roles.Employee, Roles.Facilities))
            .AddPolicy(Policies.Facilities, policy => policy
                .RequireAssertion(context => TenantClaims.Read(context.User) is not null)
                .RequireRole(Roles.Facilities));

        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();

        // Object-level checks: the handlers call IAuthorizationService with the loaded booking.
        services.AddSingleton<IAuthorizationHandler, BookingAuthorizationHandler>();

        return services;
    }
}