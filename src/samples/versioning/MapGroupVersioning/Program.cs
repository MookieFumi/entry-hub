var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Each version is a plain route group: the version is just part of the path.
var v1 = app.MapGroup("/api/v1");
v1.MapGet("/rooms", () => new[] { new RoomV1("A-101", 8) });

var v2 = app.MapGroup("/api/v2");
v2.MapGet("/rooms", () => new[] { new RoomV2("A-101", 8, Floor: 1) });

app.Run();

internal sealed record RoomV1(string Name, int Capacity);

internal sealed record RoomV2(string Name, int Capacity, int Floor);