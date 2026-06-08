using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ToDo.Domain.Entities;
using ToDo.Domain.Entities.Occurrences;
using ToDo.Domain.Entities.Plans;
using ToDo.Infrastructure.Data;

namespace ToDo.RazorLib.Services;

public sealed class LocalSyncService : ISyncService {
    private const string PairedDevicesKey = "Sync_PairedDevices";

    private readonly ISettingsService settings;
    private readonly ISyncDiscoveryService discoveryService;
    private readonly ISyncSnapshotService snapshotService;
    private readonly IDbContextFactory<TodoDbContext> contextFactory;

    public LocalSyncService(
        ISettingsService settings,
        ISyncDiscoveryService discoveryService,
        ISyncSnapshotService snapshotService,
        IDbContextFactory<TodoDbContext> contextFactory) {
        this.settings = settings;
        this.discoveryService = discoveryService;
        this.snapshotService = snapshotService;
        this.contextFactory = contextFactory;
    }

    public SyncDeviceIdentity GetLocalDevice() {
        var deviceId = settings.Get("Sync_DeviceId", "");
        if (string.IsNullOrWhiteSpace(deviceId)) {
            deviceId = Guid.NewGuid().ToString("N");
            settings.Set("Sync_DeviceId", deviceId);
        }

        var deviceName = settings.Get("Sync_DeviceName", "");
        if (string.IsNullOrWhiteSpace(deviceName)) {
            deviceName = GetDefaultDeviceName();
        }

        var syncEnabled = settings.Get("Sync_Enabled", "false") == "true";
        var autoSync = settings.Get("Sync_AutoOnChanges", "false") == "true";
        var manualAddress = settings.Get("Sync_ManualAddress", "");

        return new SyncDeviceIdentity(deviceId, deviceName, syncEnabled, syncEnabled && autoSync, manualAddress);
    }

    public void SaveLocalDevice(SyncDeviceIdentity identity) {
        var syncEnabled = identity.SyncEnabled;

        settings.Set("Sync_DeviceId", identity.DeviceId);
        settings.Set("Sync_DeviceName", identity.DeviceName.Trim());
        settings.Set("Sync_Enabled", syncEnabled.ToString().ToLowerInvariant());
        settings.Set("Sync_AutoOnChanges", (syncEnabled && identity.AutoSyncOnChanges).ToString().ToLowerInvariant());
        settings.Set("Sync_ManualAddress", identity.ManualAddress.Trim());
    }

    public async Task<SyncDiscoveryResult> DiscoverAvailableDevicesAsync(CancellationToken cancellationToken = default) {
        var pairedIds = GetPairedDevices().Select(device => device.DeviceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var discovery = await discoveryService.DiscoverAsync(GetLocalDevice(), cancellationToken);

        var availableDevices = discovery.Devices
            .Where(device => !pairedIds.Contains(device.DeviceId))
            .ToList();

        return new SyncDiscoveryResult(availableDevices, discovery.Message);
    }

    public IReadOnlyList<PairedSyncDevice> GetPairedDevices() {
        return LoadPairedDevices();
    }

    public async Task<SyncPairingSession?> StartPairingAsync(
        AvailableSyncDevice device,
        string localAddress,
        CancellationToken cancellationToken = default) {
        var localDevice = GetLocalDevice();
        using var httpClient = CreatePairingClient();

        var response = await PostJsonAsync(
            httpClient,
            $"http://{device.Address}/sync/pair/start",
            new SyncPairStartRequest(localDevice.DeviceId, localDevice.DeviceName, localAddress),
            cancellationToken);

        if (!response.IsSuccessStatusCode) {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Pair start failed: {(int)response.StatusCode} {response.ReasonPhrase}. {error}");
        }

        var pairStart = await response.Content.ReadFromJsonAsync<SyncPairStartResponse>(cancellationToken);
        if (pairStart == null || string.IsNullOrWhiteSpace(pairStart.SessionId)) {
            throw new InvalidOperationException("Pair start returned an invalid response.");
        }

        return new SyncPairingSession(
            pairStart.DeviceId,
            pairStart.DeviceName,
            pairStart.Address,
            pairStart.SessionId,
            pairStart.VerificationCode,
            pairStart.ExpiresAt,
            GenerateTrustToken());
    }

    public async Task<bool> ConfirmPairingAsync(
        SyncPairingSession session,
        string localAddress,
        CancellationToken cancellationToken = default) {
        if (session.ExpiresAt < DateTimeOffset.Now) {
            return false;
        }

        var localDevice = GetLocalDevice();
        using var httpClient = CreatePairingClient();
        var response = await PostJsonAsync(
            httpClient,
            $"http://{session.Address}/sync/pair/confirm",
            new SyncPairConfirmRequest(
                session.RemoteSessionId,
                session.VerificationCode,
                localDevice.DeviceId,
                localDevice.DeviceName,
                localAddress,
                session.TrustToken),
            cancellationToken);

        if (!response.IsSuccessStatusCode) {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Pair confirm failed: {(int)response.StatusCode} {response.ReasonPhrase}. {error}");
        }

        var pairConfirm = await response.Content.ReadFromJsonAsync<SyncPairConfirmResponse>(cancellationToken);
        if (pairConfirm == null || string.IsNullOrWhiteSpace(pairConfirm.TrustToken)) {
            return false;
        }

        var devices = LoadPairedDevices()
            .Where(device => !string.Equals(device.DeviceId, pairConfirm.DeviceId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        devices.Add(new PairedSyncDevice(
            pairConfirm.DeviceId,
            pairConfirm.DeviceName,
            pairConfirm.Address,
            DateTimeOffset.Now,
            null,
            true,
            session.TrustToken));

        SavePairedDevices(devices);
        return true;
    }

    public async Task<PairedSyncDevice> PingPairedDeviceAsync(
        PairedSyncDevice device,
        CancellationToken cancellationToken = default) {
        var localDevice = GetLocalDevice();
        using var httpClient = CreatePairingClient();
        var response = await PostJsonAsync(
            httpClient,
            $"http://{device.Address}/sync/ping",
            new SyncPingRequest(localDevice.DeviceId, device.TrustToken),
            cancellationToken);

        if (!response.IsSuccessStatusCode) {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            var offlineDevice = device with { IsOnline = false };
            AddOrReplacePairedDevice(offlineDevice);
            throw new InvalidOperationException($"Ping failed: {(int)response.StatusCode} {response.ReasonPhrase}. {error}");
        }

        var ping = await response.Content.ReadFromJsonAsync<SyncPingResponse>(cancellationToken);
        if (ping == null || !string.Equals(ping.DeviceId, device.DeviceId, StringComparison.OrdinalIgnoreCase)) {
            var offlineDevice = device with { IsOnline = false };
            AddOrReplacePairedDevice(offlineDevice);
            throw new InvalidOperationException("Ping returned an invalid device identity.");
        }

        var onlineDevice = device with {
            DeviceName = ping.DeviceName,
            IsOnline = true
        };
        AddOrReplacePairedDevice(onlineDevice);
        return onlineDevice;
    }

    public async Task<SyncSnapshotResponse> FetchSnapshotAsync(
        PairedSyncDevice device,
        CancellationToken cancellationToken = default) {
        var localDevice = GetLocalDevice();
        using var httpClient = CreatePairingClient();
        var response = await PostJsonAsync(
            httpClient,
            $"http://{device.Address}/sync/snapshot",
            new SyncSnapshotRequest(localDevice.DeviceId, device.TrustToken),
            cancellationToken);

        if (!response.IsSuccessStatusCode) {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Snapshot fetch failed: {(int)response.StatusCode} {response.ReasonPhrase}. {error}");
        }

        var snapshot = await response.Content.ReadFromJsonAsync<SyncSnapshotResponse>(cancellationToken);
        if (snapshot == null || !string.Equals(snapshot.DeviceId, device.DeviceId, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException("Snapshot returned an invalid device identity.");
        }

        return snapshot;
    }

    public async Task<SyncPreviewSummary> PreviewSyncAsync(
        PairedSyncDevice device,
        CancellationToken cancellationToken = default) {
        var remoteSnapshot = await FetchSnapshotAsync(device, cancellationToken);
        var localSnapshot = await snapshotService.CreateSnapshotAsync(GetLocalDevice(), cancellationToken);

        return CreatePreviewSummary(remoteSnapshot, localSnapshot);
    }

    public async Task<SyncImportSummary> ImportRemoteNewAsync(
        PairedSyncDevice device,
        CancellationToken cancellationToken = default) {
        var remoteSnapshot = await FetchSnapshotAsync(device, cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var importCounts = new List<SyncEntityImportCount>();

        var existingTaskDefinitionIds = await context.TaskDefinitions
            .Select(task => task.Id)
            .ToHashSetAsync(cancellationToken);
        var taskDefinitionsToAdd = remoteSnapshot.TaskDefinitions
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
        var taskListsToAdd = remoteSnapshot.TaskLists
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
        var taskListItemsToAdd = remoteSnapshot.TaskListItems
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
        var skippedTaskListItems = remoteSnapshot.TaskListItems.Count(item =>
            !existingTaskListItemIds.Contains(item.Id) &&
            (!existingTaskDefinitionIds.Contains(item.TaskDefinitionId) || !existingTaskListIds.Contains(item.TaskListId)));
        context.TaskListItems.AddRange(taskListItemsToAdd);
        AddImportCount(importCounts, "task list items", taskListItemsToAdd.Count, skippedTaskListItems);

        var dailyPlanIdsByDate = await GetPlanIdsByDateAsync(context.DailyPlans, cancellationToken);
        var weeklyPlanIdsByDate = await GetPlanIdsByDateAsync(context.WeeklyPlans, cancellationToken);
        var monthlyPlanIdsByDate = await GetPlanIdsByDateAsync(context.MonthlyPlans, cancellationToken);

        AddMissingPlans(context.DailyPlans, dailyPlanIdsByDate, remoteSnapshot.DailyOccurrences.Select(occurrence => (occurrence.Date, occurrence.DailyPlanId)));
        AddMissingPlans(context.WeeklyPlans, weeklyPlanIdsByDate, remoteSnapshot.WeeklyOccurrences.Select(occurrence => (occurrence.WeekStart, occurrence.WeeklyPlanId)));
        AddMissingPlans(context.MonthlyPlans, monthlyPlanIdsByDate, remoteSnapshot.MonthlyOccurrences.Select(occurrence => (occurrence.MonthStart, occurrence.MonthlyPlanId)));

        var existingDailyOccurrenceIds = await context.DailyOccurrences
            .Select(occurrence => occurrence.Id)
            .ToHashSetAsync(cancellationToken);
        var dailyOccurrencesToAdd = remoteSnapshot.DailyOccurrences
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
        var skippedDailyOccurrences = remoteSnapshot.DailyOccurrences.Count(occurrence =>
            !existingDailyOccurrenceIds.Contains(occurrence.Id) &&
            !existingTaskDefinitionIds.Contains(occurrence.TaskDefinitionId));
        context.DailyOccurrences.AddRange(dailyOccurrencesToAdd);
        AddImportCount(importCounts, "daily planner items", dailyOccurrencesToAdd.Count, skippedDailyOccurrences);

        var existingWeeklyOccurrenceIds = await context.WeeklyOccurrences
            .Select(occurrence => occurrence.Id)
            .ToHashSetAsync(cancellationToken);
        var weeklyOccurrencesToAdd = remoteSnapshot.WeeklyOccurrences
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
        var skippedWeeklyOccurrences = remoteSnapshot.WeeklyOccurrences.Count(occurrence =>
            !existingWeeklyOccurrenceIds.Contains(occurrence.Id) &&
            !existingTaskDefinitionIds.Contains(occurrence.TaskDefinitionId));
        context.WeeklyOccurrences.AddRange(weeklyOccurrencesToAdd);
        AddImportCount(importCounts, "weekly planner items", weeklyOccurrencesToAdd.Count, skippedWeeklyOccurrences);

        var existingMonthlyOccurrenceIds = await context.MonthlyOccurrences
            .Select(occurrence => occurrence.Id)
            .ToHashSetAsync(cancellationToken);
        var monthlyOccurrencesToAdd = remoteSnapshot.MonthlyOccurrences
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
        var skippedMonthlyOccurrences = remoteSnapshot.MonthlyOccurrences.Count(occurrence =>
            !existingMonthlyOccurrenceIds.Contains(occurrence.Id) &&
            !existingTaskDefinitionIds.Contains(occurrence.TaskDefinitionId));
        context.MonthlyOccurrences.AddRange(monthlyOccurrencesToAdd);
        AddImportCount(importCounts, "monthly planner items", monthlyOccurrencesToAdd.Count, skippedMonthlyOccurrences);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        AddOrReplacePairedDevice(device with { LastSyncedAt = DateTimeOffset.Now, IsOnline = true });

        return new SyncImportSummary(remoteSnapshot.DeviceName, importCounts);
    }

    public void RemovePairedDevice(string deviceId) {
        var devices = LoadPairedDevices()
            .Where(device => !string.Equals(device.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        SavePairedDevices(devices);
    }

    private static string GetDefaultDeviceName() {
        var machineName = Environment.MachineName;
        return string.IsNullOrWhiteSpace(machineName) ? "ToDo Device" : machineName;
    }

    private IReadOnlyList<PairedSyncDevice> LoadPairedDevices() {
        var json = settings.Get(PairedDevicesKey, "");
        if (string.IsNullOrWhiteSpace(json)) {
            return Array.Empty<PairedSyncDevice>();
        }

        try {
            return JsonSerializer.Deserialize<List<PairedSyncDevice>>(json) ?? [];
        }
        catch (JsonException) {
            return Array.Empty<PairedSyncDevice>();
        }
    }

    private void SavePairedDevices(IReadOnlyList<PairedSyncDevice> devices) {
        settings.Set(PairedDevicesKey, JsonSerializer.Serialize(devices));
    }

    private void AddOrReplacePairedDevice(PairedSyncDevice device) {
        var devices = LoadPairedDevices()
            .Where(existing => !string.Equals(existing.DeviceId, device.DeviceId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        devices.Add(device);
        SavePairedDevices(devices);
    }

    private static string GenerateTrustToken() {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static SyncPreviewSummary CreatePreviewSummary(
        SyncSnapshotResponse remoteSnapshot,
        SyncSnapshotResponse localSnapshot) {
        var conflicts = new List<SyncConflictDetail>();
        var taskNames = remoteSnapshot.TaskDefinitions
            .Concat(localSnapshot.TaskDefinitions)
            .GroupBy(task => task.Id)
            .ToDictionary(group => group.Key, group => group.First().Title);
        var listNames = remoteSnapshot.TaskLists
            .Concat(localSnapshot.TaskLists)
            .GroupBy(list => list.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

        return new SyncPreviewSummary(
            remoteSnapshot.DeviceName,
            remoteSnapshot.CreatedAt,
            [
                CompareEntities(
                    "task definitions",
                    remoteSnapshot.TaskDefinitions,
                    localSnapshot.TaskDefinitions,
                    snapshot => snapshot.Id,
                    conflicts,
                    CreateTaskDefinitionConflict),
                CompareEntities(
                    "task lists",
                    remoteSnapshot.TaskLists,
                    localSnapshot.TaskLists,
                    snapshot => snapshot.Id,
                    conflicts,
                    CreateTaskListConflict),
                CompareEntities(
                    "task list items",
                    remoteSnapshot.TaskListItems,
                    localSnapshot.TaskListItems,
                    snapshot => snapshot.Id,
                    conflicts,
                    (remote, local) => CreateTaskListItemConflict(remote, local, taskNames, listNames)),
                CompareEntities(
                    "daily planner items",
                    remoteSnapshot.DailyOccurrences,
                    localSnapshot.DailyOccurrences,
                    snapshot => snapshot.Id,
                    conflicts,
                    (remote, local) => CreateDailyOccurrenceConflict(remote, local, taskNames),
                    (remote, local) => remote.TaskDefinitionId == local.TaskDefinitionId
                        && remote.Date == local.Date
                        && remote.IsDone == local.IsDone
                        && string.Equals(remote.Timeslot, local.Timeslot, StringComparison.Ordinal)
                        && remote.SortOrder == local.SortOrder),
                CompareEntities(
                    "weekly planner items",
                    remoteSnapshot.WeeklyOccurrences,
                    localSnapshot.WeeklyOccurrences,
                    snapshot => snapshot.Id,
                    conflicts,
                    (remote, local) => CreateWeeklyOccurrenceConflict(remote, local, taskNames),
                    (remote, local) => remote.TaskDefinitionId == local.TaskDefinitionId
                        && remote.WeekStart == local.WeekStart
                        && remote.IsDone == local.IsDone
                        && remote.DayOfWeek == local.DayOfWeek),
                CompareEntities(
                    "monthly planner items",
                    remoteSnapshot.MonthlyOccurrences,
                    localSnapshot.MonthlyOccurrences,
                    snapshot => snapshot.Id,
                    conflicts,
                    (remote, local) => CreateMonthlyOccurrenceConflict(remote, local, taskNames),
                    (remote, local) => remote.TaskDefinitionId == local.TaskDefinitionId
                        && remote.MonthStart == local.MonthStart
                        && remote.IsDone == local.IsDone
                        && remote.DayOfMonth == local.DayOfMonth)
            ],
            conflicts);
    }

    private static SyncEntityPreviewCount CompareEntities<T>(
        string name,
        IReadOnlyList<T> remoteItems,
        IReadOnlyList<T> localItems,
        Func<T, Guid> getId,
        ICollection<SyncConflictDetail> conflicts,
        Func<T, T, SyncConflictDetail> createConflict,
        Func<T, T, bool>? areEqual = null) {
        var localById = localItems.ToDictionary(getId);
        var remoteIds = remoteItems.Select(getId).ToHashSet();
        var newCount = 0;
        var matchingCount = 0;
        var changedCount = 0;

        foreach (var remoteItem in remoteItems) {
            var id = getId(remoteItem);
            if (!localById.TryGetValue(id, out var localItem)) {
                newCount++;
                continue;
            }

            if ((areEqual ?? EqualityComparer<T>.Default.Equals)(remoteItem, localItem)) {
                matchingCount++;
            }
            else {
                changedCount++;
                conflicts.Add(createConflict(remoteItem, localItem));
            }
        }

        var localOnlyCount = localItems.Count(localItem => !remoteIds.Contains(getId(localItem)));
        return new SyncEntityPreviewCount(name, newCount, matchingCount, changedCount, localOnlyCount);
    }

    private static SyncConflictDetail CreateTaskDefinitionConflict(
        SyncTaskDefinitionSnapshot remote,
        SyncTaskDefinitionSnapshot local) {
        return new SyncConflictDetail(
            "task definitions",
            remote.Id,
            FirstNonEmpty(remote.Title, local.Title, remote.Id.ToString("N")),
            [
                .. FieldIfChanged("title", local.Title, remote.Title),
                .. FieldIfChanged("description", local.Description, remote.Description)
            ]);
    }

    private static SyncConflictDetail CreateTaskListConflict(
        SyncTaskListSnapshot remote,
        SyncTaskListSnapshot local) {
        return new SyncConflictDetail(
            "task lists",
            remote.Id,
            FirstNonEmpty(remote.Name, local.Name, remote.Id.ToString("N")),
            [
                .. FieldIfChanged("name", local.Name, remote.Name),
                .. FieldIfChanged("color", local.Color, remote.Color),
                .. FieldIfChanged("description", local.Description, remote.Description)
            ]);
    }

    private static SyncConflictDetail CreateTaskListItemConflict(
        SyncTaskListItemSnapshot remote,
        SyncTaskListItemSnapshot local,
        IReadOnlyDictionary<Guid, string> taskNames,
        IReadOnlyDictionary<Guid, string> listNames) {
        return new SyncConflictDetail(
            "task list items",
            remote.Id,
            $"{GetName(taskNames, remote.TaskDefinitionId)} in {GetName(listNames, remote.TaskListId)}",
            [
                .. FieldIfChanged("task", GetName(taskNames, local.TaskDefinitionId), GetName(taskNames, remote.TaskDefinitionId)),
                .. FieldIfChanged("list", GetName(listNames, local.TaskListId), GetName(listNames, remote.TaskListId)),
                .. FieldIfChanged("done", local.IsDone, remote.IsDone),
                .. FieldIfChanged("position", local.Position, remote.Position)
            ]);
    }

    private static SyncConflictDetail CreateDailyOccurrenceConflict(
        SyncDailyOccurrenceSnapshot remote,
        SyncDailyOccurrenceSnapshot local,
        IReadOnlyDictionary<Guid, string> taskNames) {
        return new SyncConflictDetail(
            "daily planner items",
            remote.Id,
            $"{GetName(taskNames, remote.TaskDefinitionId)} on {remote.Date}",
            [
                .. FieldIfChanged("task", GetName(taskNames, local.TaskDefinitionId), GetName(taskNames, remote.TaskDefinitionId)),
                .. FieldIfChanged("date", local.Date, remote.Date),
                .. FieldIfChanged("done", local.IsDone, remote.IsDone),
                .. FieldIfChanged("timeslot", local.Timeslot, remote.Timeslot),
                .. FieldIfChanged("sort order", local.SortOrder, remote.SortOrder)
            ]);
    }

    private static SyncConflictDetail CreateWeeklyOccurrenceConflict(
        SyncWeeklyOccurrenceSnapshot remote,
        SyncWeeklyOccurrenceSnapshot local,
        IReadOnlyDictionary<Guid, string> taskNames) {
        return new SyncConflictDetail(
            "weekly planner items",
            remote.Id,
            $"{GetName(taskNames, remote.TaskDefinitionId)} in week {remote.WeekStart}",
            [
                .. FieldIfChanged("task", GetName(taskNames, local.TaskDefinitionId), GetName(taskNames, remote.TaskDefinitionId)),
                .. FieldIfChanged("week", local.WeekStart, remote.WeekStart),
                .. FieldIfChanged("done", local.IsDone, remote.IsDone),
                .. FieldIfChanged("day", local.DayOfWeek, remote.DayOfWeek)
            ]);
    }

    private static SyncConflictDetail CreateMonthlyOccurrenceConflict(
        SyncMonthlyOccurrenceSnapshot remote,
        SyncMonthlyOccurrenceSnapshot local,
        IReadOnlyDictionary<Guid, string> taskNames) {
        return new SyncConflictDetail(
            "monthly planner items",
            remote.Id,
            $"{GetName(taskNames, remote.TaskDefinitionId)} in month {remote.MonthStart}",
            [
                .. FieldIfChanged("task", GetName(taskNames, local.TaskDefinitionId), GetName(taskNames, remote.TaskDefinitionId)),
                .. FieldIfChanged("month", local.MonthStart, remote.MonthStart),
                .. FieldIfChanged("done", local.IsDone, remote.IsDone),
                .. FieldIfChanged("day", local.DayOfMonth, remote.DayOfMonth)
            ]);
    }

    private static IReadOnlyList<SyncFieldConflict> FieldIfChanged<T>(
        string name,
        T localValue,
        T remoteValue) {
        if (EqualityComparer<T>.Default.Equals(localValue, remoteValue)) {
            return Array.Empty<SyncFieldConflict>();
        }

        return [new SyncFieldConflict(name, FormatValue(localValue), FormatValue(remoteValue))];
    }

    private static string GetName(IReadOnlyDictionary<Guid, string> names, Guid id) {
        return names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : id.ToString("N");
    }

    private static string FirstNonEmpty(params string[] values) {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string FormatValue<T>(T value) {
        return value switch {
            null => "(empty)",
            string text when string.IsNullOrWhiteSpace(text) => "(empty)",
            DateOnly date => date.ToString("yyyy-MM-dd"),
            bool boolean => boolean ? "yes" : "no",
            _ => value?.ToString() ?? "(empty)"
        };
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

    private static HttpClient CreatePairingClient() {
        return new HttpClient {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    private static async Task<HttpResponseMessage> PostJsonAsync<T>(
        HttpClient httpClient,
        string requestUri,
        T body,
        CancellationToken cancellationToken) {
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await httpClient.PostAsync(requestUri, content, cancellationToken);
    }
}
