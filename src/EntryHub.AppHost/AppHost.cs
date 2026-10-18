using Scalar.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

// The target for `aspire publish`: it turns the model below into a Helm chart. It does nothing when running locally.
// The Aspire dashboard is left out of the chart: it is a development tool, and the cluster has its own monitoring.
var kubernetes = builder.AddKubernetesEnvironment("k8s").WithDashboard(false);

// PostgreSQL in a container, with a volume so the data survives restarts. The database name is the one the API
// reads as ConnectionStrings:EntryHub, so the API needs no connection string of its own.
var postgres = builder.AddPostgres("postgres");
if (builder.ExecutionContext.IsRunMode)
{
    postgres.WithDataVolume();
}
else
{
    // A named Docker volume becomes an emptyDir in the chart, which is gone with the pod. A persistent volume
    // claim is what keeps the data across restarts and upgrades.
    var data = kubernetes.AddPersistentVolume("postgres-data").WithCapacity("5Gi");
    postgres.WithPersistentVolume(data, "/var/lib/postgresql");
}

var database = postgres.AddDatabase("EntryHub", "entryhub");

var api = builder.AddProject<Projects.EntryHub_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    // Over plain HTTP on purpose: the HTTPS endpoint uses the ASP.NET Core development certificate, which a
    // machine without it trusted (CI, a fresh container) rejects, and the API would never be reported healthy.
    // The readiness probe is also the health check Aspire waits on locally; in the chart it is what holds traffic
    // back until /health passes, and Kubernetes restarts the pod when /alive fails.
    .WithHttpProbe(ProbeType.Readiness, "/health", endpointName: "http")
    .WithHttpProbe(ProbeType.Liveness, "/alive", endpointName: "http")
    // The preview Kubernetes publisher writes the probe scheme in lower case ("http"), and the API server only
    // accepts "HTTP" or "HTTPS": the chart would not install. Remove once the publisher writes it correctly.
    .PublishAsKubernetesService(service =>
    {
        foreach (var container in service.Workload?.PodTemplate.Spec.Containers ?? [])
        {
            foreach (var probe in new[] { container.LivenessProbe, container.ReadinessProbe, container.StartupProbe })
            {
                if (probe?.HttpGet is { Scheme: not null } httpGet)
                {
                    httpGet.Scheme = httpGet.Scheme.ToUpperInvariant();
                }
            }
        }
    });

if (builder.ExecutionContext.IsPublishMode)
{
    // Values the cluster must supply when the chart is installed. The signing key is a secret, so it ends up in
    // the chart's secrets section and never in a ConfigMap.
    var issuer = builder.AddParameter("jwt-issuer");
    var audience = builder.AddParameter("jwt-audience");
    var signingKey = builder.AddParameter("jwt-signing-key", secret: true);

    api
        // The PostgreSQL container of the chart starts empty: the API creates the database and its schema.
        .WithEnvironment("Database__MigrateOnStartup", "true")
        .WithEnvironment("Authentication__Schemes__Bearer__ValidIssuer", issuer)
        .WithEnvironment("Authentication__Schemes__Bearer__ValidAudiences__0", audience)
        .WithEnvironment("Authentication__Schemes__Bearer__SigningKeys__0__Issuer", issuer)
        .WithEnvironment("Authentication__Schemes__Bearer__SigningKeys__0__Value", signingKey)
        // The key is 32 random bytes, base64 encoded, which is the format `dotnet user-jwts` writes.
        .WithEnvironment("Authentication__Schemes__Bearer__SigningKeys__0__Length", "32");
}

// The API applies the migrations when it starts, so the reminders service waits for it: the column it writes
// exists only after that.
builder.AddProject<Projects.EntryHub_Reminders>("reminders")
    .WithReference(database)
    .WaitFor(api);

// One API reference for every service that publishes an OpenAPI document, opened from the dashboard.
// Only the API does today; the reminders service has no HTTP endpoints.
if (builder.ExecutionContext.IsRunMode)
{
    builder.AddScalarApiReference().WithApiReference(api);
}

builder.Build().Run();