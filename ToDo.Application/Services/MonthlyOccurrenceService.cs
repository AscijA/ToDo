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

    public MonthlyOccurrenceService(IDbContextFactory<TodoDbContext> contextFactory) {
        _contextFactory = contextFactory;
    }

    public async Task<MonthlyItemDTO> GetByIdAsync(Guid occurenceId) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var monthlyItem = await context.MonthlyOccurrences.AsNoTracking()
            .Include(x => x.TaskDefinition)
            .Include(x => x.MonthlyPlan)
            .FirstOrDefaultAsync(x => x.Id == occurenceId);
        return monthlyItem?.ToMonthlyItem() ?? new();
    }

    public async Task<MonthlyItemDTO> UpdateAsync(MonthlyItemDTO dto) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var monthlyItem = await context.MonthlyOccurrences
            .Include(x => x.TaskDefinition)
            .Include(x => x.MonthlyPlan)
            .FirstOrDefaultAsync(x => x.Id == dto.OccurrenceId);
        if (monthlyItem == null) {
            throw new Exception("Monthly item not found");
        }

        monthlyItem.TaskDefinition.Title = dto.Title;
        monthlyItem.TaskDefinition.Description = dto.Description ?? string.Empty;
        monthlyItem.IsDone = dto.IsDone;
        monthlyItem.DayOfMonth = dto.DayOfMonth;
        monthlyItem.MonthlyPlan = await GetOrCreateMonthlyPlanAsync(context, dto.Date);

        await context.SaveChangesAsync();
        return monthlyItem.ToMonthlyItem();
    }

    public async Task ToggleTaskAsync(Guid occurenceId) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var monthlyItem = await context.MonthlyOccurrences.FirstOrDefaultAsync(x => x.Id == occurenceId);
        if (monthlyItem != null) {
            monthlyItem.IsDone = !monthlyItem.IsDone;
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteTaskAsync(Guid occurenceId) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var monthlyItem = await context.MonthlyOccurrences.FirstOrDefaultAsync(x => x.Id == occurenceId);
        if (monthlyItem != null) {
            context.MonthlyOccurrences.Remove(monthlyItem);
            await context.SaveChangesAsync();
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
