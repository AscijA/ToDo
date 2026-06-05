using Microsoft.EntityFrameworkCore;
using ToDo.Infrastructure.Data;

namespace ToDo.Maui.Services;

public class DatabaseInitializer {
    private readonly IDbContextFactory<TodoDbContext> _contextFactory;
    private readonly TaskCompletionSource _initializationTaskSource = new();

    public DatabaseInitializer(IDbContextFactory<TodoDbContext> contextFactory) {
        _contextFactory = contextFactory;
    }

    public Task InitializationTask => _initializationTaskSource.Task;

    public async Task InitializeAsync() {
        using var context = await _contextFactory.CreateDbContextAsync();
        //await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }
}