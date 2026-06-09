using Microsoft.EntityFrameworkCore;
using ToDo.Domain.Entities;
using ToDo.Domain.Entities.Occurrences;
using ToDo.Domain.Entities.Plans;
using ToDo.Infrastructure.Data;

namespace ToDo.RazorLib.Services;

public sealed class SyncSnapshotImportService : ISyncSnapshotImportService {
    private readonly IDbContextFactory<TodoDbContext> contextFactory;

    public SyncSnapshotImportService(IDbContextFactory<TodoDbContext> contextFactory) {
        this.contextFactory = contextFactory;
    }

    public async Task<SyncImportSummary> ImportNewAsync(
        SyncSnapshotResponse snapshot,
        CancellationToken cancellationToken = default) {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var importCounts = new List<SyncEntityImportCount>();

        var existingTaskDefinitionIds = await context.TaskDefinitions
            .Select(task => task.Id)
            .ToHashSetAsync(cancellationToken);
        var taskDefinitionsToAdd = snapshot.TaskDefinitions
            .Where(task => !existingTaskDefinitionIds.Contains(task.Id))
            .Select(task => new TaskDefinition {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description
            })
            .ToList();
        context.TaskDefinitions.AddRange(taskDefinitionsToAdd);
        AddImportCount(importCounts, "task definitions", taskDefinitionsToAdd.Count, 0);

        var existingTaskListIds = await context.TaskLists
            .Select(list => list.Id)
            .ToHashSetAsync(cancellationToken);
        var taskListsToAdd = snapshot.TaskLists
            .Where(list => !existingTaskListIds.Contains(list.Id))
            .Select(list => new TaskList {
                Id = list.Id,
                Name = list.Name,
                Color = list.Color,
                Description = list.Description
            })
            .ToList();
        context.TaskLists.AddRange(taskListsToAdd);
        AddImportCount(importCounts, "task lists", taskListsToAdd.Count, 0);

        existingTaskDefinitionIds.UnionWith(taskDefinitionsToAdd.Select(task => task.Id));
        existingTaskListIds.UnionWith(taskListsToAdd.Select(list => list.Id));

        var existingTaskListItemIds = await context.TaskListItems
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var taskListItemsToAdd = snapshot.TaskListItems
            .Where(item => !existingTaskListItemIds.Contains(item.Id))
            .Where(item => existingTaskDefinitionIds.Contains(item.TaskDefinitionId) && existingTaskListIds.Contains(item.TaskListId))
            .Select(item => new TaskListItem {
                Id = item.Id,
                TaskDefinitionId = item.TaskDefinitionId,
                TaskListId = item.TaskListId,
                IsDone = item.IsDone,
                Position = item.Position
            })
            .ToList();
        var skippedTaskListItems = snapshot.TaskListItems.Count(item =>
            !existingTaskListItemIds.Contains(item.Id) &&
            (!existingTaskDefinitionIds.Contains(item.TaskDefinitionId) || !existingTaskListIds.Contains(item.TaskListId)));
        context.TaskListItems.AddRange(taskListItemsToAdd);
        AddImportCount(importCounts, "task list items", taskListItemsToAdd.Count, skippedTaskListItems);

        var dailyPlanIdsByDate = await GetPlanIdsByDateAsync(context.DailyPlans, cancellationToken);
        var weeklyPlanIdsByDate = await GetPlanIdsByDateAsync(context.WeeklyPlans, cancellationToken);
        var monthlyPlanIdsByDate = await GetPlanIdsByDateAsync(context.MonthlyPlans, cancellationToken);

        AddMissingPlans(context.DailyPlans, dailyPlanIdsByDate, snapshot.DailyOccurrences.Select(occurrence => (occurrence.Date, occurrence.DailyPlanId)));
        AddMissingPlans(context.WeeklyPlans, weeklyPlanIdsByDate, snapshot.WeeklyOccurrences.Select(occurrence => (occurrence.WeekStart, occurrence.WeeklyPlanId)));
        AddMissingPlans(context.MonthlyPlans, monthlyPlanIdsByDate, snapshot.MonthlyOccurrences.Select(occurrence => (occurrence.MonthStart, occurrence.MonthlyPlanId)));

        var existingDailyOccurrenceIds = await context.DailyOccurrences
            .Select(occurrence => occurrence.Id)
            .ToHashSetAsync(cancellationToken);
        var dailyOccurrencesToAdd = snapshot.DailyOccurrences
            .Where(occurrence => !existingDailyOccurrenceIds.Contains(occurrence.Id))
            .Where(occurrence => existingTaskDefinitionIds.Contains(occurrence.TaskDefinitionId))
            .Select(occurrence => new DailyOccurrence {
                Id = occurrence.Id,
                TaskDefinitionId = occurrence.TaskDefinitionId,
                DailyPlanId = dailyPlanIdsByDate[occurrence.Date],
                IsDone = occurrence.IsDone,
                Timeslot = occurrence.Timeslot,
                SortOrder = occurrence.SortOrder
            })
            .ToList();
        var skippedDailyOccurrences = snapshot.DailyOccurrences.Count(occurrence =>
            !existingDailyOccurrenceIds.Contains(occurrence.Id) &&
            !existingTaskDefinitionIds.Contains(occurrence.TaskDefinitionId));
        context.DailyOccurrences.AddRange(dailyOccurrencesToAdd);
        AddImportCount(importCounts, "daily planner items", dailyOccurrencesToAdd.Count, skippedDailyOccurrences);

        var existingWeeklyOccurrenceIds = await context.WeeklyOccurrences
            .Select(occurrence => occurrence.Id)
            .ToHashSetAsync(cancellationToken);
        var weeklyOccurrencesToAdd = snapshot.WeeklyOccurrences
            .Where(occurrence => !existingWeeklyOccurrenceIds.Contains(occurrence.Id))
            .Where(occurrence => existingTaskDefinitionIds.Contains(occurrence.TaskDefinitionId))
            .Select(occurrence => new WeeklyOccurrence {
                Id = occurrence.Id,
                TaskDefinitionId = occurrence.TaskDefinitionId,
                WeeklyPlanId = weeklyPlanIdsByDate[occurrence.WeekStart],
                IsDone = occurrence.IsDone,
                DayOfWeek = occurrence.DayOfWeek
            })
            .ToList();
        var skippedWeeklyOccurrences = snapshot.WeeklyOccurrences.Count(occurrence =>
            !existingWeeklyOccurrenceIds.Contains(occurrence.Id) &&
            !existingTaskDefinitionIds.Contains(occurrence.TaskDefinitionId));
        context.WeeklyOccurrences.AddRange(weeklyOccurrencesToAdd);
        AddImportCount(importCounts, "weekly planner items", weeklyOccurrencesToAdd.Count, skippedWeeklyOccurrences);

        var existingMonthlyOccurrenceIds = await context.MonthlyOccurrences
            .Select(occurrence => occurrence.Id)
            .ToHashSetAsync(cancellationToken);
        var monthlyOccurrencesToAdd = snapshot.MonthlyOccurrences
            .Where(occurrence => !existingMonthlyOccurrenceIds.Contains(occurrence.Id))
            .Where(occurrence => existingTaskDefinitionIds.Contains(occurrence.TaskDefinitionId))
            .Select(occurrence => new MonthlyOccurrence {
                Id = occurrence.Id,
                TaskDefinitionId = occurrence.TaskDefinitionId,
                MonthlyPlanId = monthlyPlanIdsByDate[occurrence.MonthStart],
                IsDone = occurrence.IsDone,
                DayOfMonth = occurrence.DayOfMonth
            })
            .ToList();
        var skippedMonthlyOccurrences = snapshot.MonthlyOccurrences.Count(occurrence =>
            !existingMonthlyOccurrenceIds.Contains(occurrence.Id) &&
            !existingTaskDefinitionIds.Contains(occurrence.TaskDefinitionId));
        context.MonthlyOccurrences.AddRange(monthlyOccurrencesToAdd);
        AddImportCount(importCounts, "monthly planner items", monthlyOccurrencesToAdd.Count, skippedMonthlyOccurrences);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SyncImportSummary(snapshot.DeviceName, importCounts);
    }

    private static async Task<Dictionary<DateOnly, Guid>> GetPlanIdsByDateAsync<TPlan>(
        DbSet<TPlan> plans,
        CancellationToken cancellationToken)
        where TPlan : PlanBase {
        return await plans
            .Select(plan => new { plan.Date, plan.Id })
            .ToDictionaryAsync(plan => plan.Date, plan => plan.Id, cancellationToken);
    }

    private static void AddMissingPlans<TPlan>(
        DbSet<TPlan> plans,
        IDictionary<DateOnly, Guid> planIdsByDate,
        IEnumerable<(DateOnly Date, Guid PlanId)> remotePlans)
        where TPlan : PlanBase, new() {
        foreach (var remotePlan in remotePlans.DistinctBy(plan => plan.Date)) {
            if (planIdsByDate.ContainsKey(remotePlan.Date)) {
                continue;
            }

            plans.Add(new TPlan {
                Id = remotePlan.PlanId,
                Date = remotePlan.Date
            });
            planIdsByDate[remotePlan.Date] = remotePlan.PlanId;
        }
    }

    private static void AddImportCount(
        ICollection<SyncEntityImportCount> importCounts,
        string name,
        int importedCount,
        int skippedCount) {
        importCounts.Add(new SyncEntityImportCount(name, importedCount, skippedCount));
    }
}
