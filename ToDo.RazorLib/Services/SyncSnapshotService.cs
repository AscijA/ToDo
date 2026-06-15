using Microsoft.EntityFrameworkCore;
using ToDo.Infrastructure.Data;

namespace ToDo.RazorLib.Services;

public sealed class SyncSnapshotService : ISyncSnapshotService {
    private readonly IDbContextFactory<TodoDbContext> contextFactory;
    private readonly SyncChangeTracker changeTracker;

    public SyncSnapshotService(
        IDbContextFactory<TodoDbContext> contextFactory,
        SyncChangeTracker changeTracker) {
        this.contextFactory = contextFactory;
        this.changeTracker = changeTracker;
    }

    public async Task<SyncSnapshotResponse> CreateSnapshotAsync(
        SyncDeviceIdentity identity,
        CancellationToken cancellationToken = default) {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var taskDefinitions = await context.TaskDefinitions
            .AsNoTracking()
            .Select(task => new SyncTaskDefinitionSnapshot(
                task.Id,
                task.Title,
                task.Description,
                changeTracker.GetLastModified(task.Id)))
            .ToListAsync(cancellationToken);

        var taskLists = await context.TaskLists
            .AsNoTracking()
            .Select(list => new SyncTaskListSnapshot(
                list.Id,
                list.Name,
                list.Color,
                list.Description,
                list.Position,
                changeTracker.GetLastModified(list.Id)))
            .ToListAsync(cancellationToken);

        var taskListItems = await context.TaskListItems
            .AsNoTracking()
            .Select(item => new SyncTaskListItemSnapshot(
                item.Id,
                item.TaskDefinitionId,
                item.TaskListId,
                item.IsDone,
                item.Position,
                changeTracker.GetLastModified(item.Id)))
            .ToListAsync(cancellationToken);

        var dailyOccurrences = await context.DailyOccurrences
            .AsNoTracking()
            .Include(occurrence => occurrence.DailyPlan)
            .Select(occurrence => new SyncDailyOccurrenceSnapshot(
                occurrence.Id,
                occurrence.TaskDefinitionId,
                occurrence.DailyPlanId,
                occurrence.DailyPlan.Date,
                occurrence.IsDone,
                occurrence.Timeslot,
                occurrence.SortOrder,
                changeTracker.GetLastModified(occurrence.Id)))
            .ToListAsync(cancellationToken);

        var weeklyOccurrences = await context.WeeklyOccurrences
            .AsNoTracking()
            .Include(occurrence => occurrence.WeeklyPlan)
            .Select(occurrence => new SyncWeeklyOccurrenceSnapshot(
                occurrence.Id,
                occurrence.TaskDefinitionId,
                occurrence.WeeklyPlanId,
                occurrence.WeeklyPlan.Date,
                occurrence.IsDone,
                occurrence.DayOfWeek,
                changeTracker.GetLastModified(occurrence.Id)))
            .ToListAsync(cancellationToken);

        var monthlyOccurrences = await context.MonthlyOccurrences
            .AsNoTracking()
            .Include(occurrence => occurrence.MonthlyPlan)
            .Select(occurrence => new SyncMonthlyOccurrenceSnapshot(
                occurrence.Id,
                occurrence.TaskDefinitionId,
                occurrence.MonthlyPlanId,
                occurrence.MonthlyPlan.Date,
                occurrence.IsDone,
                occurrence.DayOfMonth,
                changeTracker.GetLastModified(occurrence.Id)))
            .ToListAsync(cancellationToken);

        return new SyncSnapshotResponse(
            identity.DeviceId,
            identity.DeviceName,
            "1",
            DateTimeOffset.Now,
            taskDefinitions,
            taskLists,
            taskListItems,
            dailyOccurrences,
            weeklyOccurrences,
            monthlyOccurrences,
            changeTracker.GetDeletedEntities()
                .Select(deleted => new SyncDeletedEntitySnapshot(deleted.Key, deleted.Value))
                .ToList());
    }
}
