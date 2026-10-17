using System.Text.Json.Serialization;

using EntryHub.Api.Data;
using EntryHub.Api.Features.V1;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<EntryHubDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("EntryHub")));
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddValidation();
builder.Services.AddV1Features();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Local runs create the schema on startup; other environments apply migrations as a deployment step.
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<EntryHubDbContext>().Database.MigrateAsync();
}

app.MapV1Endpoints();

app.Run();

// Makes the entry point visible to WebApplicationFactory in the test project.
public partial class Program;