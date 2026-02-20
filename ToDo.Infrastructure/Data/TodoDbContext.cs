using Microsoft.EntityFrameworkCore;
using ToDo.Domain.Entities;
using ToDo.Domain.Entities.Occurrences;

namespace ToDo.Infrastructure.Data;

public class TodoDbContext : DbContext {
    public TodoDbContext(DbContextOptions<TodoDbContext> options) : base(options) { }

    public DbSet<TaskDefinition> TaskDefinitions => Set<TaskDefinition>();
    public DbSet<DailyPlan> DailyPlans => Set<DailyPlan>();
    public DbSet<DailyOccurrence> DailyOccurrences => Set<DailyOccurrence>();
    public DbSet<WeeklyTaskState> WeeklyTaskStates => Set<WeeklyTaskState>();
    public DbSet<MonthlyTaskState> MonthlyTaskStates => Set<MonthlyTaskState>();


    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TodoDbContext).Assembly);
    }

    public override int SaveChanges() {
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) {
        return base.SaveChangesAsync(cancellationToken);
    }

}