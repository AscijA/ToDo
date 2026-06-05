using Microsoft.EntityFrameworkCore;
using ToDo.Infrastructure.Data;

namespace ToDo.Maui.Android.Services;

public class DatabaseInitializer {
    private readonly IDbContextFactory<TodoDbContext> dbContextFactory;

    public DatabaseInitializer(IDbContextFactory<TodoDbContext> dbContextFactory) {
        this.dbContextFactory = dbContextFactory;
    }

    public async Task InitializeAsync() {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        await EnsureSortOrderColumnAsync(db, "TaskLists", "Name");
        await EnsureSortOrderColumnAsync(db, "TaskListItems", "Id");
    }

    private static async Task EnsureSortOrderColumnAsync(TodoDbContext context, string tableName, string fallbackOrderColumn) {
        if (await HasColumnAsync(context, tableName, "SortOrder")) {
            return;
        }

        await context.Database.ExecuteSqlRawAsync(GetAddSortOrderSql(tableName));
        await context.Database.ExecuteSqlRawAsync(GetBackfillSortOrderSql(tableName, fallbackOrderColumn));
    }

    private static async Task<bool> HasColumnAsync(TodoDbContext context, string tableName, string columnName) {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"""PRAGMA table_info("{tableName}");""";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) {
            if (string.Equals(reader["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private static string GetAddSortOrderSql(string tableName) => tableName switch {
        "TaskLists" => """ALTER TABLE "TaskLists" ADD COLUMN "SortOrder" INTEGER NOT NULL DEFAULT 0;""",
        "TaskListItems" => """ALTER TABLE "TaskListItems" ADD COLUMN "SortOrder" INTEGER NOT NULL DEFAULT 0;""",
        _ => throw new InvalidOperationException($"Unsupported table: {tableName}")
    };

    private static string GetBackfillSortOrderSql(string tableName, string fallbackOrderColumn) => (tableName, fallbackOrderColumn) switch {
        ("TaskLists", "Name") => """
            UPDATE "TaskLists"
            SET "SortOrder" = (
                SELECT COUNT(*)
                FROM "TaskLists" AS ordered
                WHERE ordered."Name" <= "TaskLists"."Name"
            ) - 1;
            """,
        ("TaskListItems", "Id") => """
            UPDATE "TaskListItems"
            SET "SortOrder" = (
                SELECT COUNT(*)
                FROM "TaskListItems" AS ordered
                WHERE ordered."Id" <= "TaskListItems"."Id"
            ) - 1;
            """,
        _ => throw new InvalidOperationException($"Unsupported sort backfill: {tableName}.{fallbackOrderColumn}")
    };
}
