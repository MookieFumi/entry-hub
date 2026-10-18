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
    private const string ProbesTag = "Probes";

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

                AddProbes(document);

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
    /// The probes of the platform (<c>/health</c> and <c>/alive</c>) are mapped by the service defaults as plain
    /// middleware, which the generator does not see, so they are described here. They sit outside <c>/api/v1</c>
    /// on purpose: they are not part of the versioned API and answer plain text.
    /// </summary>
    private static void AddProbes(OpenApiDocument document)
    {
        document.Tags ??= new HashSet<OpenApiTag>();
        document.Tags.Add(new OpenApiTag
        {
            Name = ProbesTag,
            Description = "For Kubernetes and the Aspire AppHost, not for clients. /health is readiness: every check must pass, "
                + "PostgreSQL included, and Kubernetes holds traffic back until it does. /alive is liveness: it only says the process "
                + "responds, with no dependencies, and Kubernetes restarts the pod when it fails. A database outage must not restart "
                + "the pods, which is why the two are separate. GET /api/v1/health is the same readiness question for clients: "
                + "it answers JSON, belongs to the versioned API and is part of the contract.",
        });

        document.Paths ??= new OpenApiPaths();
        document.Paths["/health"] = Probe(
            "Readiness probe: every health check passes, PostgreSQL included.",
            "GetReadiness",
            "Healthy",
            "Unhealthy: a check failed, usually because PostgreSQL does not answer. Kubernetes stops sending traffic to the pod.");
        document.Paths["/alive"] = Probe(
            "Liveness probe: the process responds.",
            "GetLiveness",
            "Healthy",
            "Unhealthy: the process is not responding. Kubernetes restarts the pod.");
    }

    private static OpenApiPathItem Probe(string summary, string operationId, string healthy, string unhealthy) => new()
    {
        Operations = new Dictionary<HttpMethod, OpenApiOperation>
        {
            [HttpMethod.Get] = new OpenApiOperation
            {
                Tags = new HashSet<OpenApiTagReference> { new(ProbesTag) },
                Summary = summary,
                OperationId = operationId,
                Responses = new OpenApiResponses
                {
                    ["200"] = TextResponse(healthy),
                    ["503"] = TextResponse(unhealthy),
                },
            },
        },
    };

    private static OpenApiResponse TextResponse(string description) => new()
    {
        Description = description,
        Content = new Dictionary<string, OpenApiMediaType>
        {
            ["text/plain"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String } },
        },
    };

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