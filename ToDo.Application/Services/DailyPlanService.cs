using Microsoft.EntityFrameworkCore;
using ToDo.Application.Contracts.DTOs;
using ToDo.Application.Interfaces.Services;
using ToDo.Application.Mappings;
using ToDo.Domain.Entities;
using ToDo.Domain.Entities.Occurrences;
using ToDo.Domain.Entities.Plans;
using ToDo.Infrastructure.Data;

namespace ToDo.Application.Services;

public class DailyPlanService : IDailyPlanService {
    private readonly IDbContextFactory<TodoDbContext> _contextFactory;

    public DailyPlanService(IDbContextFactory<TodoDbContext> contextFactory) {
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
}