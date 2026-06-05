using Microsoft.EntityFrameworkCore;
using ToDo.Application.Contracts.DTOs;
using ToDo.Application.Interfaces.Services;
using ToDo.Infrastructure.Data;
using ToDo.Domain.Entities;

namespace ToDo.Application.Services;

public class TaskListService : ITaskListService {
    private readonly IDbContextFactory<TodoDbContext> _contextFactory;

    public TaskListService(IDbContextFactory<TodoDbContext> contextFactory) {
        _contextFactory = contextFactory;
    }

    public async Task<List<TaskListDto>> GetAllAsync() {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        var lists = await ctx.TaskLists
            .Include(l => l.Items)
                .ThenInclude(i => i.TaskDefinition)
            .ToListAsync();
        return lists.Select(l => new TaskListDto(
            l.Id, 
            l.Name, 
            l.Color, 
            l.Description, 
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
            l.Items.OrderBy(i => i.Position).Select(i => new TaskListItemDto(i.Id, i.TaskDefinitionId, i.TaskDefinition.Title, i.IsDone, i.Position)).ToList()
        );
    }

    public async Task<TaskListDto> CreateAsync(string name, string color, string? description) {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        var l = new TaskList { Name = name, Color = color, Description = description };
        ctx.TaskLists.Add(l);
        await ctx.SaveChangesAsync();
        return new TaskListDto(l.Id, l.Name, l.Color, l.Description, new List<TaskListItemDto>());
    }

    public async Task UpdateAsync(Guid id, string name, string color, string? description) {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        var l = await ctx.TaskLists.FindAsync(id);
        if (l == null) return;
        l.Name = name;
        l.Color = color;
        l.Description = description;
        await ctx.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id) {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        var l = await ctx.TaskLists.FindAsync(id);
        if (l == null) return;
        ctx.TaskLists.Remove(l);
        await ctx.SaveChangesAsync();
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
    }

    public async Task DeleteItemAsync(Guid itemId) {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        var it = await ctx.TaskListItems.FindAsync(itemId);
        if (it == null) return;
        ctx.TaskListItems.Remove(it);
        await ctx.SaveChangesAsync();
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
    }
}
