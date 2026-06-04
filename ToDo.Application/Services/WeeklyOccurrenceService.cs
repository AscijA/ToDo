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

    public WeeklyOccurrenceService(IDbContextFactory<TodoDbContext> contextFactory) {
        _contextFactory = contextFactory;
    }

    public async Task<WeeklyItemDTO> GetByIdAsync(Guid occurenceId) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var weeklyItem = await context.WeeklyOccurrences.AsNoTracking()
            .Include(x => x.TaskDefinition)
            .Include(x => x.WeeklyPlan)
            .FirstOrDefaultAsync(x => x.Id == occurenceId);
        return weeklyItem?.ToWeeklyItem() ?? new();
    }

    public async Task<WeeklyItemDTO> UpdateAsync(WeeklyItemDTO dto) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var weeklyItem = await context.WeeklyOccurrences
            .Include(x => x.TaskDefinition)
            .Include(x => x.WeeklyPlan)
            .FirstOrDefaultAsync(x => x.Id == dto.OccurrenceId);
        if (weeklyItem == null) {
            throw new Exception("Weekly item not found");
        }

        weeklyItem.TaskDefinition.Title = dto.Title;
        weeklyItem.TaskDefinition.Description = dto.Description ?? string.Empty;
        weeklyItem.IsDone = dto.IsDone;
        weeklyItem.DayOfWeek = dto.WeekDay;
        weeklyItem.WeeklyPlan = await GetOrCreateWeeklyPlanAsync(context, dto.Date);

        await context.SaveChangesAsync();
        return weeklyItem.ToWeeklyItem();
    }

    public async Task ToggleTaskAsync(Guid occurenceId) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var weeklyItem = await context.WeeklyOccurrences.FirstOrDefaultAsync(x => x.Id == occurenceId);
        if (weeklyItem != null) {
            weeklyItem.IsDone = !weeklyItem.IsDone;
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteTaskAsync(Guid occurenceId) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var weeklyItem = await context.WeeklyOccurrences.FirstOrDefaultAsync(x => x.Id == occurenceId);
        if (weeklyItem != null) {
            context.WeeklyOccurrences.Remove(weeklyItem);
            await context.SaveChangesAsync();
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
