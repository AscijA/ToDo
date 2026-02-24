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

        await context.Database.EnsureDeletedAsync();

        await context.Database.EnsureCreatedAsync();
    }
}