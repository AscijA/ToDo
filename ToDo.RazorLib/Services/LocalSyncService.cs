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
    private const string PairedDeviceTrustTokenPrefix = "Sync_PairedDeviceTrustToken_";

    private readonly ISettingsService settings;
    private readonly ISecureSettingsService secureSettings;
    private readonly ISyncDiscoveryService discoveryService;
    private readonly ISyncSnapshotService snapshotService;
    private readonly ISyncSnapshotImportService snapshotImportService;

    public LocalSyncService(
        ISettingsService settings,
        ISecureSettingsService secureSettings,
        ISyncDiscoveryService discoveryService,
        ISyncSnapshotService snapshotService,
        ISyncSnapshotImportService snapshotImportService) {
        this.settings = settings;
        this.secureSettings = secureSettings;
        this.discoveryService = discoveryService;
        this.snapshotService = snapshotService;
        this.snapshotImportService = snapshotImportService;
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

        var syncEnabled = false;
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
            var offlineDevice = CreateFailedSyncDevice(device, $"Ping failed: {(int)response.StatusCode} {response.ReasonPhrase}");
            AddOrReplacePairedDevice(offlineDevice);
            throw new InvalidOperationException($"Ping failed: {(int)response.StatusCode} {response.ReasonPhrase}. {error}");
        }

        var ping = await response.Content.ReadFromJsonAsync<SyncPingResponse>(cancellationToken);
        if (ping == null || !string.Equals(ping.DeviceId, device.DeviceId, StringComparison.OrdinalIgnoreCase)) {
            var offlineDevice = CreateFailedSyncDevice(device, "Ping returned an invalid device identity.");
            AddOrReplacePairedDevice(offlineDevice);
            throw new InvalidOperationException("Ping returned an invalid device identity.");
        }

        var onlineDevice = device with {
            DeviceName = ping.DeviceName,
            IsOnline = true,
            FailedSyncAttempts = 0,
            NextRetryAt = null,
            LastSyncError = null
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
            AddOrReplacePairedDevice(CreateFailedSyncDevice(device, $"Snapshot fetch failed: {(int)response.StatusCode} {response.ReasonPhrase}"));
            throw new InvalidOperationException($"Snapshot fetch failed: {(int)response.StatusCode} {response.ReasonPhrase}. {error}");
        }

        var snapshot = await response.Content.ReadFromJsonAsync<SyncSnapshotResponse>(cancellationToken);
        if (snapshot == null || !string.Equals(snapshot.DeviceId, device.DeviceId, StringComparison.OrdinalIgnoreCase)) {
            AddOrReplacePairedDevice(CreateFailedSyncDevice(device, "Snapshot returned an invalid device identity."));
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
        SyncImportOptions? options = null,
        CancellationToken cancellationToken = default) {
        var remoteSnapshot = await FetchSnapshotAsync(device, cancellationToken);
        var import = await snapshotImportService.ImportNewAsync(remoteSnapshot, options, cancellationToken);
        AddOrReplacePairedDevice(CreateSuccessfulSyncDevice(device));
        return import;
    }

    public async Task<SyncImportSummary> PushLocalNewAsync(
        PairedSyncDevice device,
        CancellationToken cancellationToken = default) {
        var localDevice = GetLocalDevice();
        var localSnapshot = await snapshotService.CreateSnapshotAsync(localDevice, cancellationToken);
        using var httpClient = CreatePairingClient();
        var response = await PostJsonAsync(
            httpClient,
            $"http://{device.Address}/sync/import-new",
            new SyncImportNewRequest(localDevice.DeviceId, device.TrustToken, localSnapshot),
            cancellationToken);

        if (!response.IsSuccessStatusCode) {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            AddOrReplacePairedDevice(CreateFailedSyncDevice(device, $"Push failed: {(int)response.StatusCode} {response.ReasonPhrase}"));
            throw new InvalidOperationException($"Push failed: {(int)response.StatusCode} {response.ReasonPhrase}. {error}");
        }

        var import = await response.Content.ReadFromJsonAsync<SyncImportSummary>(cancellationToken);
        if (import == null) {
            AddOrReplacePairedDevice(CreateFailedSyncDevice(device, "Push returned an invalid response."));
            throw new InvalidOperationException("Push returned an invalid response.");
        }

        AddOrReplacePairedDevice(CreateSuccessfulSyncDevice(device));
        return import;
    }

    public void RemovePairedDevice(string deviceId) {
        var devices = LoadPairedDevices()
            .Where(device => !string.Equals(device.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        secureSettings.Remove(GetTrustTokenKey(deviceId));
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
            var devices = JsonSerializer.Deserialize<List<PairedSyncDevice>>(json) ?? [];
            return devices.Select(HydrateTrustToken).ToList();
        }
        catch (JsonException) {
            return Array.Empty<PairedSyncDevice>();
        }
    }

    private void SavePairedDevices(IReadOnlyList<PairedSyncDevice> devices) {
        foreach (var device in devices.Where(device => !string.IsNullOrWhiteSpace(device.TrustToken))) {
            secureSettings.Set(GetTrustTokenKey(device.DeviceId), device.TrustToken);
        }

        var storedDevices = devices
            .Select(device => device with { TrustToken = string.Empty })
            .ToList();
        settings.Set(PairedDevicesKey, JsonSerializer.Serialize(storedDevices));
    }

    private void AddOrReplacePairedDevice(PairedSyncDevice device) {
        var devices = LoadPairedDevices()
            .Where(existing => !string.Equals(existing.DeviceId, device.DeviceId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        devices.Add(device);
        SavePairedDevices(devices);
    }

    private static PairedSyncDevice CreateSuccessfulSyncDevice(PairedSyncDevice device) {
        return device with {
            LastSyncedAt = DateTimeOffset.Now,
            IsOnline = true,
            FailedSyncAttempts = 0,
            NextRetryAt = null,
            LastSyncError = null
        };
    }

    private static PairedSyncDevice CreateFailedSyncDevice(PairedSyncDevice device, string error) {
        var attempts = Math.Min(device.FailedSyncAttempts + 1, 6);
        var delay = TimeSpan.FromMinutes(Math.Min(Math.Pow(2, attempts - 1), 30));
        return device with {
            IsOnline = false,
            FailedSyncAttempts = attempts,
            NextRetryAt = DateTimeOffset.Now.Add(delay),
            LastSyncError = error
        };
    }

    private static string GenerateTrustToken() {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private PairedSyncDevice HydrateTrustToken(PairedSyncDevice device) {
        if (!string.IsNullOrWhiteSpace(device.TrustToken)) {
            secureSettings.Set(GetTrustTokenKey(device.DeviceId), device.TrustToken);
        }

        var trustToken = secureSettings.Get(GetTrustTokenKey(device.DeviceId), device.TrustToken);
        return device with { TrustToken = trustToken };
    }

    private static string GetTrustTokenKey(string deviceId) {
        return $"{PairedDeviceTrustTokenPrefix}{deviceId}";
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
        var httpClient = new HttpClient {
            Timeout = TimeSpan.FromSeconds(5)
        };
        httpClient.DefaultRequestHeaders.ConnectionClose = true;
        return httpClient;
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
