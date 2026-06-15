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
        SyncImportOptions? options = null,
        CancellationToken cancellationToken = default) {
        var keepLocalEntityIds = options?.KeepLocalEntityIds ?? new HashSet<Guid>();
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
            if (!ShouldImportRemote(remote.Id, remote.LastModifiedAt, keepLocalEntityIds)) {
                continue;
            }

            var local = existingTaskDefinitions[remote.Id];
            local.Title = remote.Title;
            local.Description = remote.Description;
            updatedTaskDefinitions++;
        }
        importedEntityChanges.AddRange(snapshot.TaskDefinitions
            .Where(task => !existingTaskDefinitionIds.Contains(task.Id) || ShouldImportRemote(task.Id, task.LastModifiedAt, keepLocalEntityIds))
            .Select(task => (task.Id, task.LastModifiedAt)));
        AddImportCount(importCounts, "task definitions", taskDefinitionsToAdd.Count + updatedTaskDefinitions, CountKeptLocal(snapshot.TaskDefinitions, task => task.Id, keepLocalEntityIds));

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
            if (!ShouldImportRemote(remote.Id, remote.LastModifiedAt, keepLocalEntityIds)) {
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
            .Where(list => !existingTaskListIds.Contains(list.Id) || ShouldImportRemote(list.Id, list.LastModifiedAt, keepLocalEntityIds))
            .Select(list => (list.Id, list.LastModifiedAt)));
        AddImportCount(importCounts, "task lists", taskListsToAdd.Count + updatedTaskLists, CountKeptLocal(snapshot.TaskLists, list => list.Id, keepLocalEntityIds));

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
            if (!ShouldImportRemote(remote.Id, remote.LastModifiedAt, keepLocalEntityIds) ||
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
            .Where(item => (!existingTaskListItemIds.Contains(item.Id) || ShouldImportRemote(item.Id, item.LastModifiedAt, keepLocalEntityIds)) &&
                           existingTaskDefinitionIds.Contains(item.TaskDefinitionId) &&
                           existingTaskListIds.Contains(item.TaskListId))
            .Select(item => (item.Id, item.LastModifiedAt)));
        AddImportCount(importCounts, "task list items", taskListItemsToAdd.Count + updatedTaskListItems, skippedTaskListItems + CountKeptLocal(snapshot.TaskListItems, item => item.Id, keepLocalEntityIds));

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
            if (!ShouldImportRemote(remote.Id, remote.LastModifiedAt, keepLocalEntityIds) ||
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
            .Where(occurrence => (!existingDailyOccurrenceIds.Contains(occurrence.Id) || ShouldImportRemote(occurrence.Id, occurrence.LastModifiedAt, keepLocalEntityIds)) &&
                                 existingTaskDefinitionIds.Contains(occurrence.TaskDefinitionId))
            .Select(occurrence => (occurrence.Id, occurrence.LastModifiedAt)));
        AddImportCount(importCounts, "daily planner items", dailyOccurrencesToAdd.Count + updatedDailyOccurrences, skippedDailyOccurrences + CountKeptLocal(snapshot.DailyOccurrences, occurrence => occurrence.Id, keepLocalEntityIds));

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
            if (!ShouldImportRemote(remote.Id, remote.LastModifiedAt, keepLocalEntityIds) ||
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
            .Where(occurrence => (!existingWeeklyOccurrenceIds.Contains(occurrence.Id) || ShouldImportRemote(occurrence.Id, occurrence.LastModifiedAt, keepLocalEntityIds)) &&
                                 existingTaskDefinitionIds.Contains(occurrence.TaskDefinitionId))
            .Select(occurrence => (occurrence.Id, occurrence.LastModifiedAt)));
        AddImportCount(importCounts, "weekly planner items", weeklyOccurrencesToAdd.Count + updatedWeeklyOccurrences, skippedWeeklyOccurrences + CountKeptLocal(snapshot.WeeklyOccurrences, occurrence => occurrence.Id, keepLocalEntityIds));

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
            if (!ShouldImportRemote(remote.Id, remote.LastModifiedAt, keepLocalEntityIds) ||
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
            .Where(occurrence => (!existingMonthlyOccurrenceIds.Contains(occurrence.Id) || ShouldImportRemote(occurrence.Id, occurrence.LastModifiedAt, keepLocalEntityIds)) &&
                                 existingTaskDefinitionIds.Contains(occurrence.TaskDefinitionId))
            .Select(occurrence => (occurrence.Id, occurrence.LastModifiedAt)));
        AddImportCount(importCounts, "monthly planner items", monthlyOccurrencesToAdd.Count + updatedMonthlyOccurrences, skippedMonthlyOccurrences + CountKeptLocal(snapshot.MonthlyOccurrences, occurrence => occurrence.Id, keepLocalEntityIds));

        var deletedEntities = await ApplyRemoteDeletionsAsync(context, snapshot.DeletedEntities ?? [], cancellationToken);
        AddImportCount(importCounts, "deleted items", deletedEntities.Count, 0);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        changeTracker.MarkImported(importedEntityChanges);
        if (keepLocalEntityIds.Count > 0) {
            changeTracker.MarkLocalChange(keepLocalEntityIds.ToArray());
        }
        changeTracker.MarkRemoteDeleted(deletedEntities);

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

    private bool ShouldImportRemote(
        Guid entityId,
        DateTimeOffset remoteLastModifiedAt,
        IReadOnlySet<Guid> keepLocalEntityIds) {
        return !keepLocalEntityIds.Contains(entityId) && IsRemoteNewer(entityId, remoteLastModifiedAt);
    }

    private static int CountKeptLocal<T>(
        IEnumerable<T> items,
        Func<T, Guid> getId,
        IReadOnlySet<Guid> keepLocalEntityIds) {
        return items.Count(item => keepLocalEntityIds.Contains(getId(item)));
    }

    private async Task<List<SyncDeletedEntitySnapshot>> ApplyRemoteDeletionsAsync(
        TodoDbContext context,
        IReadOnlyList<SyncDeletedEntitySnapshot> deletedEntities,
        CancellationToken cancellationToken) {
        var appliedDeletions = new List<SyncDeletedEntitySnapshot>();

        foreach (var deletedEntity in deletedEntities) {
            if (!IsRemoteNewer(deletedEntity.Id, deletedEntity.DeletedAt)) {
                continue;
            }

            if (await RemoveByIdAsync(context, deletedEntity.Id, cancellationToken)) {
                appliedDeletions.Add(deletedEntity);
            }
        }

        return appliedDeletions;
    }

    private static async Task<bool> RemoveByIdAsync(
        TodoDbContext context,
        Guid id,
        CancellationToken cancellationToken) {
        var dailyOccurrence = await context.DailyOccurrences.FindAsync(new object[] { id }, cancellationToken);
        if (dailyOccurrence != null) {
            context.DailyOccurrences.Remove(dailyOccurrence);
            return true;
        }

        var weeklyOccurrence = await context.WeeklyOccurrences.FindAsync(new object[] { id }, cancellationToken);
        if (weeklyOccurrence != null) {
            context.WeeklyOccurrences.Remove(weeklyOccurrence);
            return true;
        }

        var monthlyOccurrence = await context.MonthlyOccurrences.FindAsync(new object[] { id }, cancellationToken);
        if (monthlyOccurrence != null) {
            context.MonthlyOccurrences.Remove(monthlyOccurrence);
            return true;
        }

        var taskListItem = await context.TaskListItems.FindAsync(new object[] { id }, cancellationToken);
        if (taskListItem != null) {
            context.TaskListItems.Remove(taskListItem);
            return true;
        }

        var taskList = await context.TaskLists.FindAsync(new object[] { id }, cancellationToken);
        if (taskList != null) {
            context.TaskLists.Remove(taskList);
            return true;
        }

        return false;
    }
}
