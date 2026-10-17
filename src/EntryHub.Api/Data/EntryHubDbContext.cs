using Microsoft.EntityFrameworkCore;

namespace EntryHub.Api.Data;

/// <summary>
/// Database context of the application. Entity sets are added together with the feature that needs them.
/// </summary>
public sealed class EntryHubDbContext(DbContextOptions<EntryHubDbContext> options) : DbContext(options);