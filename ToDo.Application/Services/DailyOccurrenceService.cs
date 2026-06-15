using Microsoft.EntityFrameworkCore;
using ToDo.Application.Contracts.DTOs;
using ToDo.Application.Interfaces.Services;
using ToDo.Application.Mappings;
using ToDo.Infrastructure.Data;

namespace ToDo.Application.Services;

public class DailyOccurrenceService : IDailyOccurrenceService {
    private readonly IDbContextFactory<TodoDbContext> _contextFactory;
    private readonly IDataChangeNotifier _dataChangeNotifier;

    public DailyOccurrenceService(IDbContextFactory<TodoDbContext> contextFactory, IDataChangeNotifier dataChangeNotifier) {
        _contextFactory = contextFactory;
        _dataChangeNotifier = dataChangeNotifier;
    }

    public async Task<DailyItemDTO> GetByIdAsync(Guid id) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dailyItem = await context.DailyOccurrences.AsNoTracking()
            .Include(x => x.TaskDefinition)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (dailyItem == null) return new();

        var color = await context.TaskListItems
            .Where(i => i.TaskDefinitionId == dailyItem.TaskDefinitionId)
            .Select(i => i.TaskList!.Color)
            .FirstOrDefaultAsync();

        return dailyItem!.ToDailyItem(color);
    }

    public async Task<DailyItemDTO> UpdateAsync(DailyItemDTO dto) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dailyItem = await context.DailyOccurrences
            .Include(x => x.TaskDefinition)
            .FirstOrDefaultAsync(x => x.Id == dto.OccurrenceId);
        
        if (dailyItem == null || dailyItem.TaskDefinition == null) {
            throw new Exception("Daily item or task definition not found");
        }

        dailyItem.TaskDefinition.Title = dto.Title;
        dailyItem.TaskDefinition.Description = dto.Description ?? "";
        dailyItem.IsDone = dto.IsDone;
        dailyItem.Timeslot = dto.Timeslot;

        // Synchronization logic
        var changedIds = await SyncTaskCompletionStatus(context, dailyItem.TaskDefinitionId, dto.IsDone);

        await context.SaveChangesAsync();
        _dataChangeNotifier.NotifyChanged([dailyItem.TaskDefinitionId, .. changedIds]);
        
        var color = await context.TaskListItems
            .Where(i => i.TaskDefinitionId == dailyItem.TaskDefinitionId)
            .Select(i => i.TaskList!.Color)
            .FirstOrDefaultAsync();

        return dailyItem!.ToDailyItem(color);
    }

    public async Task ToggleTaskAsync(Guid occurenceId) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dailyItem = await context.DailyOccurrences
            .FirstOrDefaultAsync(x => x.Id == occurenceId);
        if (dailyItem != null) {
            dailyItem.IsDone = !dailyItem.IsDone;
            var changedIds = await SyncTaskCompletionStatus(context, dailyItem.TaskDefinitionId, dailyItem.IsDone);
            await context.SaveChangesAsync();
            _dataChangeNotifier.NotifyChanged(changedIds.ToArray());
        }
    }

    private async Task<List<Guid>> SyncTaskCompletionStatus(TodoDbContext ctx, Guid taskDefId, bool isDone) {
        var changedIds = new List<Guid>();

        // Sync occurrences
        var daily = await ctx.DailyOccurrences.Where(o => o.TaskDefinitionId == taskDefId).ToListAsync();
        foreach (var o in daily) {
            o.IsDone = isDone;
            changedIds.Add(o.Id);
        }

        var weekly = await ctx.WeeklyOccurrences.Where(o => o.TaskDefinitionId == taskDefId).ToListAsync();
        foreach (var o in weekly) {
            o.IsDone = isDone;
            changedIds.Add(o.Id);
        }

        var monthly = await ctx.MonthlyOccurrences.Where(o => o.TaskDefinitionId == taskDefId).ToListAsync();
        foreach (var o in monthly) {
            o.IsDone = isDone;
            changedIds.Add(o.Id);
        }

        // Sync TaskList items
        var listItems = await ctx.TaskListItems.Where(i => i.TaskDefinitionId == taskDefId).ToListAsync();
        foreach (var i in listItems) {
            i.IsDone = isDone;
            changedIds.Add(i.Id);
        }

        return changedIds;
    }

    public async Task DeleteTaskAsync(Guid occurenceId) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dailyItem = await context.DailyOccurrences
            .FirstOrDefaultAsync(x => x.Id == occurenceId);
        if (dailyItem != null) {
            context.DailyOccurrences.Remove(dailyItem);
            await context.SaveChangesAsync();
            _dataChangeNotifier.NotifyDeleted(occurenceId);
        }
    }
}
