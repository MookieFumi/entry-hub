// The same application model as src/EntryHub.AppHost/AppHost.cs, written as a TypeScript AppHost.
// It points at the existing .NET projects: only the AppHost changes language, the services stay C#.
// What differs from the C# AppHost is listed in README.md.
import { createBuilder, ProbeType } from './.aspire/modules/aspire.mjs';

const builder = await createBuilder();
const executionContext = await builder.executionContext();

// PostgreSQL in a container with a data volume. The database is called "EntryHub", which is the connection
// string name the API reads, so the API needs no connection string of its own.
const postgres = await builder.addPostgres('postgres').withDataVolume();
const database = await postgres.addDatabase('EntryHub', { databaseName: 'entryhub' });

const api = await builder
    .addProject('api', '../../src/EntryHub.Api/EntryHub.Api.csproj')
    .withReference(database)
    .waitFor(database)
    // Over plain HTTP on purpose: the HTTPS endpoint uses the development certificate, which a machine
    // without it trusted rejects.
    .withHttpProbe(ProbeType.Readiness, { path: '/health', endpointName: 'http' })
    .withHttpProbe(ProbeType.Liveness, { path: '/alive', endpointName: 'http' });

if (await executionContext.isPublishMode()) {
    // Values the cluster supplies when the chart is installed. The signing key is a secret, so it ends up in
    // the chart's secrets section and never in a ConfigMap.
    const issuer = await builder.addParameter('jwt-issuer');
    const audience = await builder.addParameter('jwt-audience');
    const signingKey = await builder.addParameter('jwt-signing-key', { secret: true });

    await api
        .withEnvironment('Authentication__Schemes__Bearer__ValidIssuer', issuer)
        .withEnvironment('Authentication__Schemes__Bearer__ValidAudiences__0', audience)
        .withEnvironment('Authentication__Schemes__Bearer__SigningKeys__0__Issuer', issuer)
        .withEnvironment('Authentication__Schemes__Bearer__SigningKeys__0__Value', signingKey)
        // The key is 32 random bytes, base64 encoded, which is the format `dotnet user-jwts` writes.
        .withEnvironment('Authentication__Schemes__Bearer__SigningKeys__0__Length', '32');
}

// The API applies the migrations when it starts, so the reminders service waits for it.
await builder
    .addProject('reminders', '../../src/EntryHub.Reminders/EntryHub.Reminders.csproj')
    .withReference(database)
    .waitFor(api);

// The target for `aspire publish`. It does nothing when running locally.
await builder.addKubernetesEnvironment('k8s').withDashboard({ enabled: false });

await builder.build().run();
