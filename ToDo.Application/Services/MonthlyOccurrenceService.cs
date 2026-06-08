using Microsoft.EntityFrameworkCore;
using ToDo.Application.Contracts.DTOs;
using ToDo.Application.Interfaces.Services;
using ToDo.Application.Mappings;
using ToDo.Domain.Entities.Occurrences;
using ToDo.Domain.Entities.Plans;
using ToDo.Infrastructure.Data;

namespace ToDo.Application.Services;

public class MonthlyOccurrenceService : IMonthlyOccurrenceService {
    private readonly IDbContextFactory<TodoDbContext> _contextFactory;
    private readonly IDataChangeNotifier _dataChangeNotifier;

    public MonthlyOccurrenceService(IDbContextFactory<TodoDbContext> contextFactory, IDataChangeNotifier dataChangeNotifier) {
        _contextFactory = contextFactory;
        _dataChangeNotifier = dataChangeNotifier;
    }

    public async Task<MonthlyItemDTO> GetByIdAsync(Guid occurenceId) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var monthlyItem = await context.MonthlyOccurrences.AsNoTracking()
            .Include(x => x.TaskDefinition)
            .Include(x => x.MonthlyPlan)
            .FirstOrDefaultAsync(x => x.Id == occurenceId);
        if (monthlyItem == null) return new();

        var color = await context.TaskListItems
            .Where(i => i.TaskDefinitionId == monthlyItem.TaskDefinitionId)
            .Select(i => i.TaskList!.Color)
            .FirstOrDefaultAsync();

        return monthlyItem!.ToMonthlyItem(color);
    }

    public async Task<MonthlyItemDTO> UpdateAsync(MonthlyItemDTO dto) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var monthlyItem = await context.MonthlyOccurrences
            .Include(x => x.TaskDefinition)
            .Include(x => x.MonthlyPlan)
            .FirstOrDefaultAsync(x => x.Id == dto.OccurrenceId);
        
        if (monthlyItem == null || monthlyItem.TaskDefinition == null) {
            throw new Exception("Monthly item or task definition not found");
        }

        monthlyItem.TaskDefinition.Title = dto.Title;
        monthlyItem.TaskDefinition.Description = dto.Description ?? string.Empty;
        monthlyItem.IsDone = dto.IsDone;
        monthlyItem.DayOfMonth = dto.DayOfMonth;
        monthlyItem.MonthlyPlan = await GetOrCreateMonthlyPlanAsync(context, dto.Date);

        // Synchronization logic
        await SyncTaskCompletionStatus(context, monthlyItem.TaskDefinitionId, dto.IsDone);

        await context.SaveChangesAsync();
        _dataChangeNotifier.NotifyChanged();

        var color = await context.TaskListItems
            .Where(i => i.TaskDefinitionId == monthlyItem.TaskDefinitionId)
            .Select(i => i.TaskList!.Color)
            .FirstOrDefaultAsync();

        return monthlyItem!.ToMonthlyItem(color);
    }

    public async Task ToggleTaskAsync(Guid occurenceId) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var monthlyItem = await context.MonthlyOccurrences.FirstOrDefaultAsync(x => x.Id == occurenceId);
        if (monthlyItem != null) {
            monthlyItem.IsDone = !monthlyItem.IsDone;
            await SyncTaskCompletionStatus(context, monthlyItem.TaskDefinitionId, monthlyItem.IsDone);
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
        var monthlyItem = await context.MonthlyOccurrences.FirstOrDefaultAsync(x => x.Id == occurenceId);
        if (monthlyItem != null) {
            context.MonthlyOccurrences.Remove(monthlyItem);
            await context.SaveChangesAsync();
            _dataChangeNotifier.NotifyChanged();
        }
    }

    private static async Task<MonthlyPlan> GetOrCreateMonthlyPlanAsync(TodoDbContext context, DateOnly date) {
        var monthStart = new DateOnly(date.Year, date.Month, 1);
        var monthlyPlan = await context.MonthlyPlans.FirstOrDefaultAsync(x => x.Date == monthStart);
        if (monthlyPlan == null) {
            monthlyPlan = new MonthlyPlan {
                Date = monthStart,
                Occurrences = new List<MonthlyOccurrence>()
            };
            context.MonthlyPlans.Add(monthlyPlan);
        }

        return monthlyPlan;
    }
}
