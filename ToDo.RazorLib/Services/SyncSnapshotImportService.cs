using Microsoft.EntityFrameworkCore;
using ToDo.Domain.Entities;
using ToDo.Domain.Entities.Occurrences;
using ToDo.Domain.Entities.Plans;
using ToDo.Infrastructure.Data;

namespace ToDo.RazorLib.Services;

public sealed class SyncSnapshotImportService : ISyncSnapshotImportService {
    private readonly IDbContextFactory<TodoDbContext> contextFactory;
    private readonly SyncDataRefreshService syncDataRefresh;
    private readonly SyncChangeTracker changeTracker;

    public SyncSnapshotImportService(
        IDbContextFactory<TodoDbContext> contextFactory,
        SyncDataRefreshService syncDataRefresh,
        SyncChangeTracker changeTracker) {
        this.contextFactory = contextFactory;
        this.syncDataRefresh = syncDataRefresh;
        this.changeTracker = changeTracker;
    }

    public async Task<SyncImportSummary> ImportNewAsync(
        SyncSnapshotResponse snapshot,
        CancellationToken cancellationToken = default) {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var importCounts = new List<SyncEntityImportCount>();
        var importedEntityChanges = new List<(Guid EntityId, DateTimeOffset LastModifiedAt)>();

        var existingTaskDefinitions = await context.TaskDefinitions
            .ToDictionaryAsync(task => task.Id, cancellationToken);
        var existingTaskDefinitionIds = existingTaskDefinitions.Keys.ToHashSet();
        var taskDefinitionsToAdd = snapshot.TaskDefinitions
            .Where(task => !existingTaskDefinitionIds.Contains(task.Id))
            .Select(task => new TaskDefinition {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description
            })
            .ToList();
        context.TaskDefinitions.AddRange(taskDefinitionsToAdd);
        var updatedTaskDefinitions = 0;
        foreach (var remote in snapshot.TaskDefinitions.Where(task => existingTaskDefinitions.ContainsKey(task.Id))) {
            if (!IsRemoteNewer(remote.Id, remote.LastModifiedAt)) {
                continue;
            }

            var local = existingTaskDefinitions[remote.Id];
            local.Title = remote.Title;
            local.Description = remote.Description;
            updatedTaskDefinitions++;
        }
        importedEntityChanges.AddRange(snapshot.TaskDefinitions
            .Where(task => !existingTaskDefinitionIds.Contains(task.Id) || IsRemoteNewer(task.Id, task.LastModifiedAt))
            .Select(task => (task.Id, task.LastModifiedAt)));
        AddImportCount(importCounts, "task definitions", taskDefinitionsToAdd.Count + updatedTaskDefinitions, 0);

        var existingTaskLists = await context.TaskLists
            .ToDictionaryAsync(list => list.Id, cancellationToken);
        var existingTaskListIds = existingTaskLists.Keys.ToHashSet();
        var taskListsToAdd = snapshot.TaskLists
            .Where(list => !existingTaskListIds.Contains(list.Id))
            .Select(list => new TaskList {
                Id = list.Id,
                Name = list.Name,
                Color = list.Color,
                Description = list.Description,
                Position = list.Position
            })
            .ToList();
        context.TaskLists.AddRange(taskListsToAdd);
        var updatedTaskLists = 0;
        foreach (var remote in snapshot.TaskLists.Where(list => existingTaskLists.ContainsKey(list.Id))) {
            if (!IsRemoteNewer(remote.Id, remote.LastModifiedAt)) {
                continue;
            }

            var local = existingTaskLists[remote.Id];
            local.Name = remote.Name;
            local.Color = remote.Color;
            local.Description = remote.Description;
            local.Position = remote.Position;
            updatedTaskLists++;
        }
        importedEntityChanges.AddRange(snapshot.TaskLists
            .Where(list => !existingTaskListIds.Contains(list.Id) || IsRemoteNewer(list.Id, list.LastModifiedAt))
            .Select(list => (list.Id, list.LastModifiedAt)));
        AddImportCount(importCounts, "task lists", taskListsToAdd.Count + updatedTaskLists, 0);

        existingTaskDefinitionIds.UnionWith(taskDefinitionsToAdd.Select(task => task.Id));
        existingTaskListIds.UnionWith(taskListsToAdd.Select(list => list.Id));

        var existingTaskListItems = await context.TaskListItems
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var existingTaskListItemIds = existingTaskListItems.Keys.ToHashSet();
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
        var updatedTaskListItems = 0;
        foreach (var remote in snapshot.TaskListItems.Where(item => existingTaskListItems.ContainsKey(item.Id))) {
            if (!IsRemoteNewer(remote.Id, remote.LastModifiedAt) ||
                !existingTaskDefinitionIds.Contains(remote.TaskDefinitionId) ||
                !existingTaskListIds.Contains(remote.TaskListId)) {
                continue;
            }

            var local = existingTaskListItems[remote.Id];
            local.TaskDefinitionId = remote.TaskDefinitionId;
            local.TaskListId = remote.TaskListId;
            local.IsDone = remote.IsDone;
            local.Position = remote.Position;
            updatedTaskListItems++;
        }
        importedEntityChanges.AddRange(snapshot.TaskListItems
            .Where(item => (!existingTaskListItemIds.Contains(item.Id) || IsRemoteNewer(item.Id, item.LastModifiedAt)) &&
                           existingTaskDefinitionIds.Contains(item.TaskDefinitionId) &&
                           existingTaskListIds.Contains(item.TaskListId))
            .Select(item => (item.Id, item.LastModifiedAt)));
        AddImportCount(importCounts, "task list items", taskListItemsToAdd.Count + updatedTaskListItems, skippedTaskListItems);

        var dailyPlanIdsByDate = await GetPlanIdsByDateAsync(context.DailyPlans, cancellationToken);
        var weeklyPlanIdsByDate = await GetPlanIdsByDateAsync(context.WeeklyPlans, cancellationToken);
        var monthlyPlanIdsByDate = await GetPlanIdsByDateAsync(context.MonthlyPlans, cancellationToken);

        AddMissingPlans(context.DailyPlans, dailyPlanIdsByDate, snapshot.DailyOccurrences.Select(occurrence => (occurrence.Date, occurrence.DailyPlanId)));
        AddMissingPlans(context.WeeklyPlans, weeklyPlanIdsByDate, snapshot.WeeklyOccurrences.Select(occurrence => (occurrence.WeekStart, occurrence.WeeklyPlanId)));
        AddMissingPlans(context.MonthlyPlans, monthlyPlanIdsByDate, snapshot.MonthlyOccurrences.Select(occurrence => (occurrence.MonthStart, occurrence.MonthlyPlanId)));

        var existingDailyOccurrences = await context.DailyOccurrences
            .ToDictionaryAsync(occurrence => occurrence.Id, cancellationToken);
        var existingDailyOccurrenceIds = existingDailyOccurrences.Keys.ToHashSet();
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
        var updatedDailyOccurrences = 0;
        foreach (var remote in snapshot.DailyOccurrences.Where(occurrence => existingDailyOccurrences.ContainsKey(occurrence.Id))) {
            if (!IsRemoteNewer(remote.Id, remote.LastModifiedAt) ||
                !existingTaskDefinitionIds.Contains(remote.TaskDefinitionId)) {
                continue;
            }

            var local = existingDailyOccurrences[remote.Id];
            local.TaskDefinitionId = remote.TaskDefinitionId;
            local.DailyPlanId = dailyPlanIdsByDate[remote.Date];
            local.IsDone = remote.IsDone;
            local.Timeslot = remote.Timeslot;
            local.SortOrder = remote.SortOrder;
            updatedDailyOccurrences++;
        }
        importedEntityChanges.AddRange(snapshot.DailyOccurrences
            .Where(occurrence => (!existingDailyOccurrenceIds.Contains(occurrence.Id) || IsRemoteNewer(occurrence.Id, occurrence.LastModifiedAt)) &&
                                 existingTaskDefinitionIds.Contains(occurrence.TaskDefinitionId))
            .Select(occurrence => (occurrence.Id, occurrence.LastModifiedAt)));
        AddImportCount(importCounts, "daily planner items", dailyOccurrencesToAdd.Count + updatedDailyOccurrences, skippedDailyOccurrences);

        var existingWeeklyOccurrences = await context.WeeklyOccurrences
            .ToDictionaryAsync(occurrence => occurrence.Id, cancellationToken);
        var existingWeeklyOccurrenceIds = existingWeeklyOccurrences.Keys.ToHashSet();
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
        var updatedWeeklyOccurrences = 0;
        foreach (var remote in snapshot.WeeklyOccurrences.Where(occurrence => existingWeeklyOccurrences.ContainsKey(occurrence.Id))) {
            if (!IsRemoteNewer(remote.Id, remote.LastModifiedAt) ||
                !existingTaskDefinitionIds.Contains(remote.TaskDefinitionId)) {
                continue;
            }

            var local = existingWeeklyOccurrences[remote.Id];
            local.TaskDefinitionId = remote.TaskDefinitionId;
            local.WeeklyPlanId = weeklyPlanIdsByDate[remote.WeekStart];
            local.IsDone = remote.IsDone;
            local.DayOfWeek = remote.DayOfWeek;
            updatedWeeklyOccurrences++;
        }
        importedEntityChanges.AddRange(snapshot.WeeklyOccurrences
            .Where(occurrence => (!existingWeeklyOccurrenceIds.Contains(occurrence.Id) || IsRemoteNewer(occurrence.Id, occurrence.LastModifiedAt)) &&
                                 existingTaskDefinitionIds.Contains(occurrence.TaskDefinitionId))
            .Select(occurrence => (occurrence.Id, occurrence.LastModifiedAt)));
        AddImportCount(importCounts, "weekly planner items", weeklyOccurrencesToAdd.Count + updatedWeeklyOccurrences, skippedWeeklyOccurrences);

        var existingMonthlyOccurrences = await context.MonthlyOccurrences
            .ToDictionaryAsync(occurrence => occurrence.Id, cancellationToken);
        var existingMonthlyOccurrenceIds = existingMonthlyOccurrences.Keys.ToHashSet();
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
        var updatedMonthlyOccurrences = 0;
        foreach (var remote in snapshot.MonthlyOccurrences.Where(occurrence => existingMonthlyOccurrences.ContainsKey(occurrence.Id))) {
            if (!IsRemoteNewer(remote.Id, remote.LastModifiedAt) ||
                !existingTaskDefinitionIds.Contains(remote.TaskDefinitionId)) {
                continue;
            }

            var local = existingMonthlyOccurrences[remote.Id];
            local.TaskDefinitionId = remote.TaskDefinitionId;
            local.MonthlyPlanId = monthlyPlanIdsByDate[remote.MonthStart];
            local.IsDone = remote.IsDone;
            local.DayOfMonth = remote.DayOfMonth;
            updatedMonthlyOccurrences++;
        }
        importedEntityChanges.AddRange(snapshot.MonthlyOccurrences
            .Where(occurrence => (!existingMonthlyOccurrenceIds.Contains(occurrence.Id) || IsRemoteNewer(occurrence.Id, occurrence.LastModifiedAt)) &&
                                 existingTaskDefinitionIds.Contains(occurrence.TaskDefinitionId))
            .Select(occurrence => (occurrence.Id, occurrence.LastModifiedAt)));
        AddImportCount(importCounts, "monthly planner items", monthlyOccurrencesToAdd.Count + updatedMonthlyOccurrences, skippedMonthlyOccurrences);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        changeTracker.MarkImported(importedEntityChanges);

        var import = new SyncImportSummary(snapshot.DeviceName, importCounts);
        if (import.ImportedCount > 0) {
            syncDataRefresh.NotifyChanged();
        }

        return import;
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

    private bool IsRemoteNewer(Guid entityId, DateTimeOffset remoteLastModifiedAt) {
        return remoteLastModifiedAt > changeTracker.GetLastModified(entityId);
    }
}
