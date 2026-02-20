using Microsoft.EntityFrameworkCore;
using ToDo.Infrastructure.Data;

namespace ToDo.Maui.Services;

public class DatabaseInitializer {
    private readonly TodoDbContext _context;

    public DatabaseInitializer(TodoDbContext context) {
        _context = context;
    }

    public async Task InitializeAsync() {
        await _context.Database.MigrateAsync();
    }
}