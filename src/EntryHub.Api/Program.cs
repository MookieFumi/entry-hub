using EntryHub.Api.Data;
using EntryHub.Api.Features.V1;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<EntryHubDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("EntryHub")));
builder.Services.AddV1Features();

var app = builder.Build();

app.MapV1Endpoints();

app.Run();

// Makes the entry point visible to WebApplicationFactory in the test project.
public partial class Program;