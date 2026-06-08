using Microsoft.EntityFrameworkCore;
using ToDo.Infrastructure.Data;

namespace ToDo.RazorLib.Services;

public sealed class SyncSnapshotService : ISyncSnapshotService {
    private readonly IDbContextFactory<TodoDbContext> contextFactory;

    public SyncSnapshotService(IDbContextFactory<TodoDbContext> contextFactory) {
        this.contextFactory = contextFactory;
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
                task.Description))
            .ToListAsync(cancellationToken);

        var taskLists = await context.TaskLists
            .AsNoTracking()
            .Select(list => new SyncTaskListSnapshot(
                list.Id,
                list.Name,
                list.Color,
                list.Description))
            .ToListAsync(cancellationToken);

        var taskListItems = await context.TaskListItems
            .AsNoTracking()
            .Select(item => new SyncTaskListItemSnapshot(
                item.Id,
                item.TaskDefinitionId,
                item.TaskListId,
                item.IsDone,
                item.Position))
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
                occurrence.SortOrder))
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
                occurrence.DayOfWeek))
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
                occurrence.DayOfMonth))
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
            monthlyOccurrences);
    }
}
