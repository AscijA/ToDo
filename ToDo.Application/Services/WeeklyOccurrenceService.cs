using Microsoft.EntityFrameworkCore;
using ToDo.Application.Contracts.DTOs;
using ToDo.Application.Interfaces.Services;
using ToDo.Application.Mappings;
using ToDo.Domain.Entities.Occurrences;
using ToDo.Domain.Entities.Plans;
using ToDo.Infrastructure.Data;

namespace ToDo.Application.Services;

public class WeeklyOccurrenceService : IWeeklyOccurrenceService {
    private readonly IDbContextFactory<TodoDbContext> _contextFactory;
    private readonly IDataChangeNotifier _dataChangeNotifier;

    public WeeklyOccurrenceService(IDbContextFactory<TodoDbContext> contextFactory, IDataChangeNotifier dataChangeNotifier) {
        _contextFactory = contextFactory;
        _dataChangeNotifier = dataChangeNotifier;
    }

    public async Task<WeeklyItemDTO> GetByIdAsync(Guid occurenceId) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var weeklyItem = await context.WeeklyOccurrences.AsNoTracking()
            .Include(x => x.TaskDefinition)
            .Include(x => x.WeeklyPlan)
            .FirstOrDefaultAsync(x => x.Id == occurenceId);
        if (weeklyItem == null) return new();

        var color = await context.TaskListItems
            .Where(i => i.TaskDefinitionId == weeklyItem.TaskDefinitionId)
            .Select(i => i.TaskList!.Color)
            .FirstOrDefaultAsync();

        return weeklyItem!.ToWeeklyItem(color);
    }

    public async Task<WeeklyItemDTO> UpdateAsync(WeeklyItemDTO dto) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var weeklyItem = await context.WeeklyOccurrences
            .Include(x => x.TaskDefinition)
            .Include(x => x.WeeklyPlan)
            .FirstOrDefaultAsync(x => x.Id == dto.OccurrenceId);
        
        if (weeklyItem == null || weeklyItem.TaskDefinition == null) {
            throw new Exception("Weekly item or task definition not found");
        }

        weeklyItem.TaskDefinition.Title = dto.Title;
        weeklyItem.TaskDefinition.Description = dto.Description ?? string.Empty;
        weeklyItem.IsDone = dto.IsDone;
        weeklyItem.DayOfWeek = dto.WeekDay;
        weeklyItem.WeeklyPlan = await GetOrCreateWeeklyPlanAsync(context, dto.Date);

        // Synchronization logic
        await SyncTaskCompletionStatus(context, weeklyItem.TaskDefinitionId, dto.IsDone);

        await context.SaveChangesAsync();
        _dataChangeNotifier.NotifyChanged();

        var color = await context.TaskListItems
            .Where(i => i.TaskDefinitionId == weeklyItem.TaskDefinitionId)
            .Select(i => i.TaskList!.Color)
            .FirstOrDefaultAsync();

        return weeklyItem!.ToWeeklyItem(color);
    }

    public async Task ToggleTaskAsync(Guid occurenceId) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var weeklyItem = await context.WeeklyOccurrences.FirstOrDefaultAsync(x => x.Id == occurenceId);
        if (weeklyItem != null) {
            weeklyItem.IsDone = !weeklyItem.IsDone;
            await SyncTaskCompletionStatus(context, weeklyItem.TaskDefinitionId, weeklyItem.IsDone);
            await context.SaveChangesAsync();
            _dataChangeNotifier.NotifyChanged();
        }
    }

    private async Task SyncTaskCompletionStatus(TodoDbContext ctx, Guid taskDefId, bool isDone) {
        var daily = await ctx.DailyOccurrences.Where(o => o.TaskDefinitionId == taskDefId).ToListAsync();
        foreach (var o in daily) o.IsDone = isDone;

        var weekly = await ctx.WeeklyOccurrences.Where(o => o.TaskDefinitionId == taskDefId).ToListAsync();
        foreach (var o in weekly) o.IsDone = isDone;

        var monthly = await ctx.MonthlyOccurrences.Where(o => o.TaskDefinitionId == taskDefId).ToListAsync();
        foreach (var o in monthly) o.IsDone = isDone;

        var listItems = await ctx.TaskListItems.Where(i => i.TaskDefinitionId == taskDefId).ToListAsync();
        foreach (var i in listItems) i.IsDone = isDone;
    }

    public async Task DeleteTaskAsync(Guid occurenceId) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var weeklyItem = await context.WeeklyOccurrences.FirstOrDefaultAsync(x => x.Id == occurenceId);
        if (weeklyItem != null) {
            context.WeeklyOccurrences.Remove(weeklyItem);
            await context.SaveChangesAsync();
            _dataChangeNotifier.NotifyChanged();
        }
    }

    private static async Task<WeeklyPlan> GetOrCreateWeeklyPlanAsync(TodoDbContext context, DateOnly date) {
        var weekStart = GetWeekStart(date);
        var weeklyPlan = await context.WeeklyPlans.FirstOrDefaultAsync(x => x.Date == weekStart);
        if (weeklyPlan == null) {
            weeklyPlan = new WeeklyPlan {
                Date = weekStart,
                Occurrences = new List<WeeklyOccurrence>()
            };
            context.WeeklyPlans.Add(weeklyPlan);
        }

        return weeklyPlan;
    }

    private static DateOnly GetWeekStart(DateOnly date) {
        var diff = (7 + ((int)date.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff);
    }
}
