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
        
        var query = from d in context.DailyOccurrences.AsNoTracking()
                    where d.DailyPlan.Date == date
                    orderby d.Timeslot == null, d.Timeslot
                    select new {
                        Occurrence = d,
                        TaskDefinition = d.TaskDefinition,
                        Color = context.TaskListItems
                            .Where(i => i.TaskDefinitionId == d.TaskDefinitionId)
                            .Select(i => i.TaskList.Color)
                            .FirstOrDefault()
                    };

        var results = await query.ToListAsync();
        return results.Select(r => r.Occurrence.ToDailyItem(r.Color)).ToList();
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
            SortOrder = dailyPlan.Occurrences.Count
        };
        dailyOccurrence = context.DailyOccurrences.Add(dailyOccurrence).Entity;
        await context.SaveChangesAsync();
        
        var color = await context.TaskListItems
            .Where(i => i.TaskDefinitionId == dailyOccurrence.TaskDefinitionId)
            .Select(i => i.TaskList.Color)
            .FirstOrDefaultAsync();

        return dailyOccurrence.ToDailyItem(color);
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
                catch (ArgumentOutOfRangeException) {
                    // Invalid day for month (e.g. 31st in February) - skip
                }
            }
        }
        if (changed) {
            await context.SaveChangesAsync();
        }

        var query = from w in context.WeeklyOccurrences.AsNoTracking()
                    where w.WeeklyPlan.Date == weekStart
                    orderby w.DayOfWeek == null, w.DayOfWeek, w.TaskDefinition.Title
                    select new {
                        Occurrence = w,
                        TaskDefinition = w.TaskDefinition,
                        Color = context.TaskListItems
                            .Where(i => i.TaskDefinitionId == w.TaskDefinitionId)
                            .Select(i => i.TaskList.Color)
                            .FirstOrDefault()
                    };

        var results = await query.ToListAsync();
        return results.Select(r => r.Occurrence.ToWeeklyItem(r.Color)).ToList();
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
            .Select(i => i.TaskList.Color)
            .FirstOrDefaultAsync();

        return weeklyOccurrence.ToWeeklyItem(color);
    }

    public async Task<List<MonthlyItemDTO>> GetAllMonthlyAsync(DateOnly date) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var monthStart = new DateOnly(date.Year, date.Month, 1);
        
        var query = from m in context.MonthlyOccurrences.AsNoTracking()
                    where m.MonthlyPlan.Date == monthStart
                    orderby m.DayOfMonth == null, m.DayOfMonth, m.TaskDefinition.Title
                    select new {
                        Occurrence = m,
                        TaskDefinition = m.TaskDefinition,
                        Color = context.TaskListItems
                            .Where(i => i.TaskDefinitionId == m.TaskDefinitionId)
                            .Select(i => i.TaskList.Color)
                            .FirstOrDefault()
                    };

        var results = await query.ToListAsync();
        return results.Select(r => r.Occurrence.ToMonthlyItem(r.Color)).ToList();
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
            .Select(i => i.TaskList.Color)
            .FirstOrDefaultAsync();

        return monthlyOccurrence.ToMonthlyItem(color);
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
