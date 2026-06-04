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
        return await context.DailyOccurrences.AsNoTracking()
            .Include(d => d.TaskDefinition)
            .Where(d => d.DailyPlan.Date == date)
            .OrderBy(d => d.SortOrder)
            .Select(d => d.ToDailyItem())
            .ToListAsync();
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

        var taskDef = CreateTaskDefinition(dto.Title, dto.Description);
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

    public async Task<List<WeeklyItemDTO>> GetAllWeeklyAsync(DateOnly date) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var weekStart = GetWeekStart(date);
        return await context.WeeklyOccurrences.AsNoTracking()
            .Include(w => w.TaskDefinition)
            .Include(w => w.WeeklyPlan)
            .Where(w => w.WeeklyPlan.Date == weekStart)
            .OrderBy(w => w.DayOfWeek == null)
            .ThenBy(w => w.DayOfWeek)
            .ThenBy(w => w.TaskDefinition.Title)
            .Select(w => w.ToWeeklyItem())
            .ToListAsync();
    }

    public async Task<WeeklyItemDTO> AddTaskToWeekAsync(DateOnly date, WeeklyItemDTO dto) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var weekStart = GetWeekStart(date);
        var weeklyPlan = await GetOrCreateWeeklyPlanAsync(context, weekStart);
        var taskDef = CreateTaskDefinition(dto.Title, dto.Description);
        context.TaskDefinitions.Add(taskDef);

        var weeklyOccurrence = new WeeklyOccurrence {
            WeeklyPlan = weeklyPlan,
            TaskDefinitionId = dto.TaskDefinitionId,
            DayOfWeek = dto.WeekDay,
            IsDone = false,
            TaskDefinition = taskDef
        };
        weeklyOccurrence = context.WeeklyOccurrences.Add(weeklyOccurrence).Entity;
        await context.SaveChangesAsync();
        return weeklyOccurrence.ToWeeklyItem();
    }

    public async Task<List<MonthlyItemDTO>> GetAllMonthlyAsync(DateOnly date) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var monthStart = new DateOnly(date.Year, date.Month, 1);
        return await context.MonthlyOccurrences.AsNoTracking()
            .Include(m => m.TaskDefinition)
            .Include(m => m.MonthlyPlan)
            .Where(m => m.MonthlyPlan.Date == monthStart)
            .OrderBy(m => m.DayOfMonth == null)
            .ThenBy(m => m.DayOfMonth)
            .ThenBy(m => m.TaskDefinition.Title)
            .Select(m => m.ToMonthlyItem())
            .ToListAsync();
    }

    public async Task<MonthlyItemDTO> AddTaskToMonthAsync(DateOnly date, MonthlyItemDTO dto) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var monthStart = new DateOnly(date.Year, date.Month, 1);
        var monthlyPlan = await GetOrCreateMonthlyPlanAsync(context, monthStart);
        var taskDef = CreateTaskDefinition(dto.Title, dto.Description);
        context.TaskDefinitions.Add(taskDef);

        var monthlyOccurrence = new MonthlyOccurrence {
            MonthlyPlan = monthlyPlan,
            TaskDefinitionId = dto.TaskDefinitionId,
            DayOfMonth = dto.DayOfMonth,
            IsDone = false,
            TaskDefinition = taskDef
        };
        monthlyOccurrence = context.MonthlyOccurrences.Add(monthlyOccurrence).Entity;
        await context.SaveChangesAsync();
        return monthlyOccurrence.ToMonthlyItem();
    }

    private static TaskDefinition CreateTaskDefinition(string title, string? description) {
        return new TaskDefinition {
            Title = title,
            Description = description ?? string.Empty
        };
    }

    private static DateOnly GetWeekStart(DateOnly date) {
        var diff = (7 + ((int)date.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff);
    }

    private static async Task<WeeklyPlan> GetOrCreateWeeklyPlanAsync(TodoDbContext context, DateOnly weekStart) {
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

    private static async Task<MonthlyPlan> GetOrCreateMonthlyPlanAsync(TodoDbContext context, DateOnly monthStart) {
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
