using System.Security.Cryptography;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ToDo.RazorLib.Services;

public sealed class LocalSyncService : ISyncService {
    private const string PairedDevicesKey = "Sync_PairedDevices";

    private readonly ISettingsService settings;
    private readonly ISyncDiscoveryService discoveryService;

    public LocalSyncService(ISettingsService settings, ISyncDiscoveryService discoveryService) {
        this.settings = settings;
        this.discoveryService = discoveryService;
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
            pairConfirm.TrustToken));

        SavePairedDevices(devices);
        return true;
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

    private static string GenerateTrustToken() {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
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
