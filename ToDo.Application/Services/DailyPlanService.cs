#pragma warning disable CS8602
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
        
        var items = await context.DailyOccurrences.AsNoTracking()
            .Include(d => d.TaskDefinition)
            .Where(d => d.DailyPlan.Date == date)
            .OrderBy(d => d.Timeslot == null)
            .ThenBy(d => d.Timeslot)
            .ToListAsync();

        var taskDefIds = items.Select(i => i.TaskDefinitionId).Distinct().ToList();
        var colorMap = await context.TaskListItems.AsNoTracking()
            .Where(i => taskDefIds.Contains(i.TaskDefinitionId))
            .Select(i => new { i.TaskDefinitionId, i.TaskList.Color })
            .ToListAsync();
        
        var colors = colorMap.GroupBy(x => x.TaskDefinitionId)
                             .ToDictionary(g => g.Key, g => g.First()!.Color);

        var results = new List<DailyItemDTO>();
        foreach (var i in items) {
            if (i != null)
#pragma warning disable CS8602
                results.Add(i.ToDailyItem(colors.GetValueOrDefault(i.TaskDefinitionId)));
#pragma warning restore CS8602
        }
        return results;
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

        TaskDefinition? taskDef = null;
        if (dto.TaskDefinitionId != Guid.Empty) {
            taskDef = await context.TaskDefinitions.FindAsync(dto.TaskDefinitionId);
        }

        if (taskDef == null) {
            taskDef = CreateTaskDefinition(dto.Title, dto.Description);
            context.TaskDefinitions.Add(taskDef);
        }

        var dailyOccurrence = new DailyOccurrence {
            DailyPlan = dailyPlan,
            TaskDefinition = taskDef,
            Timeslot = dto.Timeslot,
            IsDone = dto.IsDone,
            SortOrder = dailyPlan.Occurrences?.Count ?? 0
        };
        dailyOccurrence = context.DailyOccurrences.Add(dailyOccurrence).Entity;
        await context.SaveChangesAsync();
        
        var color = await context.TaskListItems
            .Where(i => i.TaskDefinitionId == dailyOccurrence.TaskDefinitionId)
            .Select(i => i.TaskList!.Color)
            .FirstOrDefaultAsync();

        return dailyOccurrence!.ToDailyItem(color);
    }

    public async Task<List<WeeklyItemDTO>> GetAllWeeklyAsync(DateOnly date) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var weekStart = GetWeekStart(date);
        var weekEnd = weekStart.AddDays(6);

        // Feature 2: Move Monthly to Weekly
        var relevantMonths = new List<DateOnly> {
            new DateOnly(weekStart.Year, weekStart.Month, 1),
            new DateOnly(weekEnd.Year, weekEnd.Month, 1)
        }.Distinct().ToList();

        bool changed = false;
        foreach (var monthStart in relevantMonths) {
            var monthlyOccurrences = await context.MonthlyOccurrences
                .Include(m => m.TaskDefinition)
                .Where(m => m.MonthlyPlan.Date == monthStart && m.DayOfMonth != null)
                .ToListAsync();

            foreach (var m in monthlyOccurrences) {
                try {
                    var occurrenceDate = new DateOnly(monthStart.Year, monthStart.Month, m.DayOfMonth!.Value);
                    if (occurrenceDate >= weekStart && occurrenceDate <= weekEnd) {
                        var weeklyPlan = await GetOrCreateWeeklyPlanAsync(context, weekStart);
                        var weeklyOccurrence = new WeeklyOccurrence {
                            WeeklyPlan = weeklyPlan,
                            TaskDefinitionId = m.TaskDefinitionId,
                            DayOfWeek = occurrenceDate.DayOfWeek,
                            IsDone = m.IsDone,
                            TaskDefinition = m.TaskDefinition
                        };
                        context.WeeklyOccurrences.Add(weeklyOccurrence);
                        context.MonthlyOccurrences.Remove(m);
                        changed = true;
                    }
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }
        if (changed) await context.SaveChangesAsync();

        var items = await context.WeeklyOccurrences.AsNoTracking()
            .Include(w => w.TaskDefinition)
            .Include(w => w.WeeklyPlan)
            .Where(w => w.WeeklyPlan.Date == weekStart)
            .OrderBy(w => w.DayOfWeek == null)
            .ThenBy(w => w.DayOfWeek)
            .ThenBy(w => w.TaskDefinition.Title)
            .ToListAsync();

        var taskDefIds = items.Select(i => i.TaskDefinitionId).Distinct().ToList();
        var colorMap = await context.TaskListItems.AsNoTracking()
            .Where(i => taskDefIds.Contains(i.TaskDefinitionId))
            .Select(i => new { i.TaskDefinitionId, i.TaskList.Color })
            .ToListAsync();

        var colors = colorMap.GroupBy(x => x.TaskDefinitionId)
                             .ToDictionary(g => g.Key, g => g.First()!.Color);

        var results = new List<WeeklyItemDTO>();
        foreach (var i in items) {
            if (i != null)
#pragma warning disable CS8602
                results.Add(i.ToWeeklyItem(colors.GetValueOrDefault(i.TaskDefinitionId)));
#pragma warning restore CS8602
        }
        return results;
    }

    public async Task<WeeklyItemDTO> AddTaskToWeekAsync(DateOnly date, WeeklyItemDTO dto) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var weekStart = GetWeekStart(date);
        var weeklyPlan = await GetOrCreateWeeklyPlanAsync(context, weekStart);
        
        TaskDefinition? taskDef = null;
        if (dto.TaskDefinitionId != Guid.Empty) {
            taskDef = await context.TaskDefinitions.FindAsync(dto.TaskDefinitionId);
        }

        if (taskDef == null) {
            taskDef = CreateTaskDefinition(dto.Title, dto.Description);
            context.TaskDefinitions.Add(taskDef);
        }

        var weeklyOccurrence = new WeeklyOccurrence {
            WeeklyPlan = weeklyPlan,
            TaskDefinition = taskDef,
            DayOfWeek = dto.WeekDay,
            IsDone = dto.IsDone
        };
        weeklyOccurrence = context.WeeklyOccurrences.Add(weeklyOccurrence).Entity;
        await context.SaveChangesAsync();

        var color = await context.TaskListItems
            .Where(i => i.TaskDefinitionId == weeklyOccurrence.TaskDefinitionId)
            .Select(i => i.TaskList!.Color)
            .FirstOrDefaultAsync();

        return weeklyOccurrence!.ToWeeklyItem(color);
    }

    public async Task<List<MonthlyItemDTO>> GetAllMonthlyAsync(DateOnly date) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var monthStart = new DateOnly(date.Year, date.Month, 1);
        
        var items = await context.MonthlyOccurrences.AsNoTracking()
            .Include(m => m.TaskDefinition)
            .Include(m => m.MonthlyPlan)
            .Where(m => m.MonthlyPlan.Date == monthStart)
            .OrderBy(m => m.DayOfMonth == null)
            .ThenBy(m => m.DayOfMonth)
            .ThenBy(m => m.TaskDefinition.Title)
            .ToListAsync();

        var taskDefIds = items.Select(i => i.TaskDefinitionId).Distinct().ToList();
        var colorMap = await context.TaskListItems.AsNoTracking()
            .Where(i => taskDefIds.Contains(i.TaskDefinitionId))
            .Select(i => new { i.TaskDefinitionId, i.TaskList.Color })
            .ToListAsync();

        var colors = colorMap.GroupBy(x => x.TaskDefinitionId)
                             .ToDictionary(g => g.Key, g => g.First()!.Color);

        var results = new List<MonthlyItemDTO>();
        foreach (var i in items) {
            if (i != null)
#pragma warning disable CS8602
                results.Add(i.ToMonthlyItem(colors.GetValueOrDefault(i.TaskDefinitionId)));
#pragma warning restore CS8602
        }
        return results;
    }

    public async Task<MonthlyItemDTO> AddTaskToMonthAsync(DateOnly date, MonthlyItemDTO dto) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var monthStart = new DateOnly(date.Year, date.Month, 1);
        var monthlyPlan = await GetOrCreateMonthlyPlanAsync(context, monthStart);
        
        TaskDefinition? taskDef = null;
        if (dto.TaskDefinitionId != Guid.Empty) {
            taskDef = await context.TaskDefinitions.FindAsync(dto.TaskDefinitionId);
        }

        if (taskDef == null) {
            taskDef = CreateTaskDefinition(dto.Title, dto.Description);
            context.TaskDefinitions.Add(taskDef);
        }

        var monthlyOccurrence = new MonthlyOccurrence {
            MonthlyPlan = monthlyPlan,
            TaskDefinition = taskDef,
            DayOfMonth = dto.DayOfMonth,
            IsDone = dto.IsDone
        };
        monthlyOccurrence = context.MonthlyOccurrences.Add(monthlyOccurrence).Entity;
        await context.SaveChangesAsync();

        var color = await context.TaskListItems
            .Where(i => i.TaskDefinitionId == monthlyOccurrence.TaskDefinitionId)
            .Select(i => i.TaskList!.Color)
            .FirstOrDefaultAsync();

        return monthlyOccurrence!.ToMonthlyItem(color);
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
