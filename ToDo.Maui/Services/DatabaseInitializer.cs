using Microsoft.EntityFrameworkCore;
using ToDo.Infrastructure.Data;

namespace ToDo.Maui.Services;

public class DatabaseInitializer {
    private readonly IDbContextFactory<TodoDbContext> _contextFactory;

    public DatabaseInitializer(IDbContextFactory<TodoDbContext> contextFactory) {
        _contextFactory = contextFactory;
    }

    public async Task InitializeAsync() {
        using var context = await _contextFactory.CreateDbContextAsync();

        // For development/prototype: if schema changes, we need to recreate
        // In a real app, we would use migrations.
        try {
            // Try a simple query to see if the schema matches
            await context.TaskLists.AnyAsync();
        }
        catch {
            // If it fails (likely due to missing columns), recreate
            await context.Database.EnsureDeletedAsync();
        }

        await context.Database.EnsureCreatedAsync();
    }
}