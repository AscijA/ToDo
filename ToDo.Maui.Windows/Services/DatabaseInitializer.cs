using Microsoft.EntityFrameworkCore;
using ToDo.Infrastructure.Data;

namespace ToDo.Maui.Windows.Services;

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
        await EnsureSchemaUpdatesAsync(context);
    }

    private static async Task EnsureSchemaUpdatesAsync(TodoDbContext context) {
        var conn = context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();   
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('TaskLists') WHERE name='Position'";
        var exists = Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;
        if (!exists) {
            cmd.CommandText = "ALTER TABLE TaskLists ADD COLUMN Position INTEGER NOT NULL DEFAULT 0";
            await cmd.ExecuteNonQueryAsync();
        }
    }
}