using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// One OpenAPI document per version. Each one keeps only the endpoints under its own prefix.
builder.Services.AddOpenApi("v1", options =>
{
    options.ShouldInclude = description => description.RelativePath?.StartsWith("api/v1/", StringComparison.Ordinal) == true;

    // Version 1 is on its way out: every operation of its document is marked as deprecated.
    options.AddOperationTransformer((operation, _, _) =>
    {
        operation.Deprecated = true;
        return Task.CompletedTask;
    });
});
builder.Services.AddOpenApi("v2", options =>
    options.ShouldInclude = description => description.RelativePath?.StartsWith("api/v2/", StringComparison.Ordinal) == true);

var app = builder.Build();

// Each version is a plain route group: the version is just part of the path.
var v1 = app.MapGroup("/api/v1");
v1.MapGet("/rooms", () => new[] { new RoomV1("A-101", 8) });

var v2 = app.MapGroup("/api/v2");
v2.MapGet("/rooms", () => new[] { new RoomV2("A-101", 8, Floor: 1) });

// /openapi/v1.json and /openapi/v2.json, and one Scalar page with a selector to switch between them.
app.MapOpenApi();
app.MapScalarApiReference(options => options
    .AddDocument("v2", "Version 2", isDefault: true)
    .AddDocument("v1", "Version 1 (deprecated)"));

app.Run();

internal sealed record RoomV1(string Name, int Capacity);

internal sealed record RoomV2(string Name, int Capacity, int Floor);