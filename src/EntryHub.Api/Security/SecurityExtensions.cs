using Microsoft.AspNetCore.Authorization;

namespace EntryHub.Api.Security;

public static class SecurityExtensions
{
    /// <summary>
    /// JWT Bearer authentication read from the <c>Authentication:Schemes:Bearer</c> section, which is where
    /// <c>dotnet user-jwts</c> writes the issuer and audience and where it keeps the signing key (user secrets).
    /// </summary>
    public static IServiceCollection AddEntryHubSecurity(this IServiceCollection services)
    {
        services
            .AddAuthentication()
            .AddJwtBearer(options =>
            {
                // Keep the claim names of the token ("sub", "role") instead of renaming them to long URIs.
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = "sub";
                options.TokenValidationParameters.RoleClaimType = "role";

                // By default an expired token is still accepted for five more minutes (TokenValidationParameters
                // .DefaultClockSkew). Thirty seconds covers the clock drift between servers without that window.
                options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
            });

        services
            .AddAuthorizationBuilder()
            .AddPolicy(Policies.AnyEmployee, policy => policy.RequireRole(Roles.Employee, Roles.Facilities))
            .AddPolicy(Policies.Facilities, policy => policy.RequireRole(Roles.Facilities));

        // Object-level checks: the handlers call IAuthorizationService with the loaded booking.
        services.AddSingleton<IAuthorizationHandler, BookingAuthorizationHandler>();

        return services;
    }
}