using System.Text.Json.Serialization;

using EntryHub.Api.Data;
using EntryHub.Api.Documentation;
using EntryHub.Api.Errors;
using EntryHub.Api.Features;
using EntryHub.Api.Features.V1;
using EntryHub.Api.Security;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<EntryHubDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("EntryHub")));

// Adds Npgsql tracing, metrics and a health check. Retries stay off: CreateBookingHandler already decides what
// a deadlock between two simultaneous bookings means, and a silent retry would hide it.
builder.EnrichNpgsqlDbContext<EntryHubDbContext>(settings => settings.DisableRetry = true);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddValidation();
// Every error the framework writes (validation, unhandled exceptions) becomes application/problem+json.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DatabaseUnavailableHandler>();
builder.Services.AddEntryHubSecurity();
builder.Services.AddEntryHubOpenApi();
builder.Services.AddFeatureHandlers();
builder.Services.AddV1Features(builder.Configuration);

var app = builder.Build();

// First in the pipeline, so it catches what everything after it throws. Exceptions no handler takes become a 500
// problem details without the stack trace, which stays in the logs.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    // Local runs create the schema on startup; other environments apply migrations as a deployment step.
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<EntryHubDbContext>().Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapV1Endpoints();
app.MapEntryHubDocumentation();

app.Run();

// Makes the entry point visible to WebApplicationFactory in the test project.
public partial class Program;