var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL in a container, with a volume so the data survives restarts. The database name is the one the API
// reads as ConnectionStrings:EntryHub, so the API needs no connection string of its own.
var postgres = builder.AddPostgres("postgres").WithDataVolume();
var database = postgres.AddDatabase("EntryHub", "entryhub");

var api = builder.AddProject<Projects.EntryHub_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health");

// The API applies the migrations when it starts, so the reminders service waits for it: the column it writes
// exists only after that.
builder.AddProject<Projects.EntryHub_Reminders>("reminders")
    .WithReference(database)
    .WaitFor(api);

builder.Build().Run();