using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ToDo.Infrastructure.Data;

public class TodoDbContextFactory : IDesignTimeDbContextFactory<TodoDbContext> {
    public TodoDbContext CreateDbContext(string[] args) {
        var optionsBuilder = new DbContextOptionsBuilder<TodoDbContext>();
        optionsBuilder.UseSqlite("Data Source=todo_design.db");

        return new TodoDbContext(optionsBuilder.Options);
    }
}
