using System.Security.Cryptography;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ToDo.RazorLib.Services;

public sealed class LocalSyncService : ISyncService {
    private const string PairedDevicesKey = "Sync_PairedDevices";

    private readonly ISettingsService settings;
    private readonly ISyncDiscoveryService discoveryService;
    private readonly ISyncSnapshotService snapshotService;

    public LocalSyncService(
        ISettingsService settings,
        ISyncDiscoveryService discoveryService,
        ISyncSnapshotService snapshotService) {
        this.settings = settings;
        this.discoveryService = discoveryService;
        this.snapshotService = snapshotService;
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
        return new SyncPreviewSummary(
            remoteSnapshot.DeviceName,
            remoteSnapshot.CreatedAt,
            [
                CompareEntities(
                    "task definitions",
                    remoteSnapshot.TaskDefinitions,
                    localSnapshot.TaskDefinitions,
                    snapshot => snapshot.Id),
                CompareEntities(
                    "task lists",
                    remoteSnapshot.TaskLists,
                    localSnapshot.TaskLists,
                    snapshot => snapshot.Id),
                CompareEntities(
                    "task list items",
                    remoteSnapshot.TaskListItems,
                    localSnapshot.TaskListItems,
                    snapshot => snapshot.Id),
                CompareEntities(
                    "daily planner items",
                    remoteSnapshot.DailyOccurrences,
                    localSnapshot.DailyOccurrences,
                    snapshot => snapshot.Id),
                CompareEntities(
                    "weekly planner items",
                    remoteSnapshot.WeeklyOccurrences,
                    localSnapshot.WeeklyOccurrences,
                    snapshot => snapshot.Id),
                CompareEntities(
                    "monthly planner items",
                    remoteSnapshot.MonthlyOccurrences,
                    localSnapshot.MonthlyOccurrences,
                    snapshot => snapshot.Id)
            ]);
    }

    private static SyncEntityPreviewCount CompareEntities<T>(
        string name,
        IReadOnlyList<T> remoteItems,
        IReadOnlyList<T> localItems,
        Func<T, Guid> getId) {
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

            if (EqualityComparer<T>.Default.Equals(remoteItem, localItem)) {
                matchingCount++;
            }
            else {
                changedCount++;
            }
        }

        var localOnlyCount = localItems.Count(localItem => !remoteIds.Contains(getId(localItem)));
        return new SyncEntityPreviewCount(name, newCount, matchingCount, changedCount, localOnlyCount);
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
