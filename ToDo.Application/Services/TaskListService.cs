using Microsoft.EntityFrameworkCore;
using ToDo.Application.Contracts.DTOs;
using ToDo.Application.Interfaces.Services;
using ToDo.Infrastructure.Data;
using ToDo.Domain.Entities;

namespace ToDo.Application.Services;

public class TaskListService : ITaskListService {
    private readonly IDbContextFactory<TodoDbContext> _contextFactory;
    private readonly IDataChangeNotifier _dataChangeNotifier;

    public TaskListService(IDbContextFactory<TodoDbContext> contextFactory, IDataChangeNotifier dataChangeNotifier) {
        _contextFactory = contextFactory;
        _dataChangeNotifier = dataChangeNotifier;
    }

    public async Task<List<TaskListDto>> GetAllAsync() {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        var lists = await ctx.TaskLists
            .Include(l => l.Items)
                .ThenInclude(i => i.TaskDefinition)
            .OrderBy(l => l.Position)
            .ToListAsync();
        return lists.Select(l => new TaskListDto(
            l.Id, 
            l.Name, 
            l.Color, 
            l.Description, 
            l.Position,
            l.Items.OrderBy(i => i.Position).Select(i => new TaskListItemDto(i.Id, i.TaskDefinitionId, i.TaskDefinition.Title, i.IsDone, i.Position)).ToList()
        )).ToList();
    }

    public async Task<TaskListDto?> GetByIdAsync(Guid id) {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        var l = await ctx.TaskLists
            .Include(x => x.Items)
                .ThenInclude(i => i.TaskDefinition)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (l == null) return null;
        return new TaskListDto(
            l.Id, 
            l.Name, 
            l.Color, 
            l.Description, 
            l.Position,
            l.Items.OrderBy(i => i.Position).Select(i => new TaskListItemDto(i.Id, i.TaskDefinitionId, i.TaskDefinition.Title, i.IsDone, i.Position)).ToList()
        );
    }

    public async Task<TaskListDto> CreateAsync(string name, string color, string? description) {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        var maxPosition = await ctx.TaskLists.AnyAsync()
            ? await ctx.TaskLists.MaxAsync(t => t.Position)
            : -1;
        var l = new TaskList { Name = name, Color = color, Description = description, Position = maxPosition + 1 };
        ctx.TaskLists.Add(l);
        await ctx.SaveChangesAsync();
        _dataChangeNotifier.NotifyChanged(l.Id);
        return new TaskListDto(l.Id, l.Name, l.Color, l.Description, l.Position, new List<TaskListItemDto>());
    }

    public async Task UpdateAsync(Guid id, string name, string color, string? description) {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        var l = await ctx.TaskLists.FindAsync(id);
        if (l == null) return;
        l.Name = name;
        l.Color = color;
        l.Description = description;
        await ctx.SaveChangesAsync();
        _dataChangeNotifier.NotifyChanged(l.Id);
    }

    public async Task DeleteAsync(Guid id) {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        var l = await ctx.TaskLists.FindAsync(id);
        if (l == null) return;
        ctx.TaskLists.Remove(l);
        await ctx.SaveChangesAsync();
        _dataChangeNotifier.NotifyDeleted(id);
    }

    public async Task<Guid> CreateTaskAsync(string title, string? description, IReadOnlyCollection<Guid> taskListIds) {
        using var ctx = await _contextFactory.CreateDbContextAsync();

        var taskDef = new TaskDefinition {
            Title = title,
            Description = description ?? string.Empty
        };
        ctx.TaskDefinitions.Add(taskDef);

        foreach (var listId in taskListIds.Distinct()) {
            var maxPosition = await ctx.TaskListItems
                .Where(i => i.TaskListId == listId)
                .Select(i => (int?)i.Position)
                .MaxAsync() ?? -1;

            ctx.TaskListItems.Add(new TaskListItem {
                TaskDefinition = taskDef,
                TaskListId = listId,
                Position = maxPosition + 1
            });
        }

        await ctx.SaveChangesAsync();
        _dataChangeNotifier.NotifyChanged();

        return taskDef.Id;
    }

    public async Task<TaskListItemDto> AddItemAsync(Guid listId, string text) {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        
        var maxPosition = await ctx.TaskListItems
            .Where(i => i.TaskListId == listId)
            .Select(i => (int?)i.Position)
            .MaxAsync() ?? -1;

        var taskDef = new TaskDefinition { Title = text, Description = "" };
        ctx.TaskDefinitions.Add(taskDef);

        var li = new TaskListItem { TaskDefinition = taskDef, TaskListId = listId, Position = maxPosition + 1 };
        ctx.TaskListItems.Add(li);
        await ctx.SaveChangesAsync();
        _dataChangeNotifier.NotifyChanged(taskDef.Id, li.Id);
        return new TaskListItemDto(li.Id, taskDef.Id, taskDef.Title, li.IsDone, li.Position);
    }

    public async Task UpdateItemAsync(TaskListItemDto item) {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        var li = await ctx.TaskListItems.Include(i => i.TaskDefinition).FirstOrDefaultAsync(i => i.Id == item.Id);
        if (li == null) return;
        li.TaskDefinition.Title = item.Text;
        li.IsDone = item.IsDone;
        li.Position = item.Position;

        // Synchronization logic
        var taskDefId = li.TaskDefinitionId;
        var isDone = item.IsDone;

        var daily = await ctx.DailyOccurrences.Where(o => o.TaskDefinitionId == taskDefId).ToListAsync();
        foreach (var o in daily) o.IsDone = isDone;

        var weekly = await ctx.WeeklyOccurrences.Where(o => o.TaskDefinitionId == taskDefId).ToListAsync();
        foreach (var o in weekly) o.IsDone = isDone;

        var monthly = await ctx.MonthlyOccurrences.Where(o => o.TaskDefinitionId == taskDefId).ToListAsync();
        foreach (var o in monthly) o.IsDone = isDone;

        await ctx.SaveChangesAsync();
        _dataChangeNotifier.NotifyChanged(
            [li.Id, li.TaskDefinitionId, .. daily.Select(o => o.Id), .. weekly.Select(o => o.Id), .. monthly.Select(o => o.Id)]);
    }

    public async Task DeleteItemAsync(Guid itemId) {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        var it = await ctx.TaskListItems.FindAsync(itemId);
        if (it == null) return;
        ctx.TaskListItems.Remove(it);
        await ctx.SaveChangesAsync();
        _dataChangeNotifier.NotifyDeleted(itemId);
    }

    public async Task UpdateOrderAsync(Guid listId, List<Guid> itemIds) {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        var items = await ctx.TaskListItems.Where(i => i.TaskListId == listId).ToListAsync();

        for (int i = 0; i < itemIds.Count; i++) {
            var item = items.FirstOrDefault(x => x.Id == itemIds[i]);
            if (item != null) {
                item.Position = i;
            }
        }

        await ctx.SaveChangesAsync();
        _dataChangeNotifier.NotifyChanged(items.Select(item => item.Id).ToArray());
    }

    public async Task ReorderListsAsync(List<Guid> listIds) {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        var lists = await ctx.TaskLists.ToListAsync();

        for (int i = 0; i < listIds.Count; i++) {
            var list = lists.FirstOrDefault(x => x.Id == listIds[i]);
            if (list != null) {
                list.Position = i;
            }
        }

        await ctx.SaveChangesAsync();
        _dataChangeNotifier.NotifyChanged(lists.Select(list => list.Id).ToArray());
    }
}
