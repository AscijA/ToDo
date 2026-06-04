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
        var lists = await ctx.TaskLists.Include(l => l.Items).ToListAsync();
        return lists.Select(l => new TaskListDto(l.Id, l.Name, l.Color, l.Description, l.Items.Select(i => new TaskListItemDto(i.Id, i.Text, i.IsDone)).ToList())).ToList();
    }

    public async Task<TaskListDto?> GetByIdAsync(Guid id) {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        var l = await ctx.TaskLists.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (l == null) return null;
        return new TaskListDto(l.Id, l.Name, l.Color, l.Description, l.Items.Select(i => new TaskListItemDto(i.Id, i.Text, i.IsDone)).ToList());
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
        var li = new TaskListItem { Text = text, TaskListId = listId };
        ctx.TaskListItems.Add(li);
        await ctx.SaveChangesAsync();
        return new TaskListItemDto(li.Id, li.Text, li.IsDone);
    }

    public async Task UpdateItemAsync(TaskListItemDto item) {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        var li = await ctx.TaskListItems.FindAsync(item.Id);
        if (li == null) return;
        li.Text = item.Text;
        li.IsDone = item.IsDone;
        await ctx.SaveChangesAsync();
    }

    public async Task DeleteItemAsync(Guid itemId) {
        using var ctx = await _contextFactory.CreateDbContextAsync();
        var it = await ctx.TaskListItems.FindAsync(itemId);
        if (it == null) return;
        ctx.TaskListItems.Remove(it);
        await ctx.SaveChangesAsync();
    }
}
