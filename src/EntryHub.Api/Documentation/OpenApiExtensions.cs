using EntryHub.Api.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

using Scalar.AspNetCore;

namespace EntryHub.Api.Documentation;

/// <summary>
/// The OpenAPI document of version 1 of the API and the pages that show it.
/// </summary>
public static class OpenApiExtensions
{
    private const string DocumentName = "v1";
    private const string BearerScheme = "Bearer";

    public static IServiceCollection AddEntryHubOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "EntryHub API",
                    Version = "1.0",
                    Description = "Room booking: rooms, bookings with business rules and a live occupancy stream.",
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "A JWT with the role Employee or Facilities. In development: dotnet user-jwts create.",
                };

                return Task.CompletedTask;
            });

            // Only the operations that need a token get the lock icon, so health stays visibly public.
            options.AddOperationTransformer((operation, context, _) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                var needsToken = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();

                if (needsToken)
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [],
                    });
                }

                return Task.CompletedTask;
            });
        });

    /// <summary>
    /// Serves the document as JSON and YAML. Outside Development both need the Facilities role, so the contract
    /// is not public by accident. The interactive page (Scalar) exists only in Development.
    /// </summary>
    public static void MapEntryHubDocumentation(this WebApplication app)
    {
        var json = app.MapOpenApi("/openapi/{documentName}.json");
        var yaml = app.MapOpenApi("/openapi/{documentName}.yaml");

        if (!app.Environment.IsDevelopment())
        {
            json.RequireAuthorization(Policies.Facilities);
            yaml.RequireAuthorization(Policies.Facilities);
            return;
        }

        app.MapScalarApiReference(options => options
            .WithTitle("EntryHub API")
            .AddPreferredSecuritySchemes(BearerScheme));
    }
}
