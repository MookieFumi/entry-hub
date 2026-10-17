using Asp.Versioning;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiVersioning(options =>
{
    // Adds the api-supported-versions and api-deprecated-versions headers to every response.
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

var app = builder.Build();

var versions = app.NewApiVersionSet()
    .HasDeprecatedApiVersion(new ApiVersion(1, 0))
    .HasApiVersion(new ApiVersion(2, 0))
    .Build();

var api = app.MapGroup("/api/v{version:apiVersion}").WithApiVersionSet(versions);

api.MapGet("/rooms", () => new[] { new RoomV1("A-101", 8) }).MapToApiVersion(1, 0);
api.MapGet("/rooms", () => new[] { new RoomV2("A-101", 8, Floor: 1) }).MapToApiVersion(2, 0);

app.Run();

internal sealed record RoomV1(string Name, int Capacity);

internal sealed record RoomV2(string Name, int Capacity, int Floor);