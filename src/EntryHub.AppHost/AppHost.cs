var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL in a container, with a volume so the data survives restarts. The database name is the one the API
// reads as ConnectionStrings:EntryHub, so the API needs no connection string of its own.
var postgres = builder.AddPostgres("postgres").WithDataVolume();
var database = postgres.AddDatabase("EntryHub", "entryhub");

builder.AddProject<Projects.EntryHub_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health");

builder.Build().Run();