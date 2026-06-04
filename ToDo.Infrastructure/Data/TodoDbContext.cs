using Microsoft.EntityFrameworkCore;
using ToDo.Domain.Entities;
using ToDo.Domain.Entities.Occurrences;
using ToDo.Domain.Entities.Plans;

namespace ToDo.Infrastructure.Data;

public class TodoDbContext : DbContext {
    public TodoDbContext(DbContextOptions<TodoDbContext> options) : base(options) { }

    public DbSet<TaskDefinition> TaskDefinitions => Set<TaskDefinition>();
    public DbSet<DailyPlan> DailyPlans => Set<DailyPlan>();
    public DbSet<WeeklyPlan> WeeklyPlans => Set<WeeklyPlan>();
    public DbSet<MonthlyPlan> MonthlyPlans => Set<MonthlyPlan>();
    public DbSet<DailyOccurrence> DailyOccurrences => Set<DailyOccurrence>();
    public DbSet<WeeklyOccurrence> WeeklyOccurrences => Set<WeeklyOccurrence>();
    public DbSet<MonthlyOccurrence> MonthlyOccurrences => Set<MonthlyOccurrence>();
    public DbSet<TaskList> TaskLists => Set<TaskList>();
    public DbSet<TaskListItem> TaskListItems => Set<TaskListItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TodoDbContext).Assembly);
    }

}