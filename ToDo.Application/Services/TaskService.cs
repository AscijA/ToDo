using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using ToDo.Application.Contracts.DTOs;
using ToDo.Application.Interfaces.Services;
using ToDo.Application.Mappings;
using ToDo.Domain.Entities;
using ToDo.Domain.Entities.Occurrences;
using ToDo.Infrastructure.Data;

namespace ToDo.Application.Services;

public class TaskService : ITaskService {
    private readonly IDbContextFactory<TodoDbContext> _contextFactory;

    public TaskService(IDbContextFactory<TodoDbContext> contextFactory) {
        _contextFactory = contextFactory;
    }


    public async Task<List<DailyItemDTO>> GetAllDailyOfDateAsync(DateOnly date) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dailyItem = await context.DailyOccurrences.AsNoTracking()
            .Include(d => d.TaskDefinition)
            .Where(d => d.DailyPlan.Date == date)
            .OrderBy(d => d.SortOrder)
            .Select(d => d.ToDailyItem())
            .ToListAsync();

        return dailyItem;
    }

    public async Task<DailyItemDTO> GetByIdAsync(Guid id) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dailyItem = await context.DailyOccurrences.AsNoTracking()
            .Include(x => x.TaskDefinition)
            .FirstOrDefaultAsync(x => x.Id == id);
        return dailyItem?.ToDailyItem() ?? new();
    }
    public async Task<DailyItemDTO> UpdateAsync(DailyItemDTO dto) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dailyItem = await context.DailyOccurrences
            .Include(x => x.TaskDefinition)
            .FirstOrDefaultAsync(x => x.Id == dto.OccurrenceId);
        if (dailyItem == null) {
            throw new Exception("Daily item not found");
        }

        dailyItem.TaskDefinition.Title = dto.Title;
        dailyItem.TaskDefinition.Description = dto.Description ?? "";
        dailyItem.IsDone = dto.IsDone;
        dailyItem.Timeslot = dto.Timeslot;
        await context.SaveChangesAsync();

        return dailyItem.ToDailyItem();
    }

    public async Task ToggleTaskAsync(Guid occurenceId) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dailyItem = await context.DailyOccurrences
            .FirstOrDefaultAsync(x => x.Id == occurenceId);
        if (dailyItem != null) {
            dailyItem.IsDone = !dailyItem.IsDone;
            await context.SaveChangesAsync();
        }
    }

    public async Task<DailyItemDTO> AddTaskToDateAsync(DateOnly date, DailyItemDTO dto) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dailyPlan = await context.DailyPlans
            .FirstOrDefaultAsync(x => x.Date == date);
        if (dailyPlan == null) {
            dailyPlan = new DailyPlan {
                Date = date,
                Occurrences = new List<DailyOccurrence>()
            };
            context.DailyPlans.Add(dailyPlan);
        }

        // provjerit jel guid ima
        var taskDef = new TaskDefinition {
            Title = dto.Title,
            Description = dto.Description ?? ""
        };
        context.TaskDefinitions.Add(taskDef);

        var dailyOccurrence = new DailyOccurrence {
            DailyPlan = dailyPlan,
            TaskDefinitionId = dto.TaskDefinitionId,
            Timeslot = dto.Timeslot,
            IsDone = false,
            SortOrder = dailyPlan.Occurrences.Count,
            TaskDefinition = taskDef
        };
        dailyOccurrence = context.DailyOccurrences.Add(dailyOccurrence).Entity;
        await context.SaveChangesAsync();
        return dailyOccurrence.ToDailyItem();
    }

    public async Task DeleteTaskAsync(Guid occurenceId) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dailyItem = await context.DailyOccurrences
            .FirstOrDefaultAsync(x => x.Id == occurenceId);
        if (dailyItem != null) {
            context.DailyOccurrences.Remove(dailyItem);
            await context.SaveChangesAsync();
        }
    }
}
