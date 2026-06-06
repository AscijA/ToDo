using System.Security.Cryptography;
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

    public SyncPairingSession StartPairing(AvailableSyncDevice device) {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        code = $"{code[..3]}-{code[3..]}";

        return new SyncPairingSession(
            device.DeviceId,
            device.DeviceName,
            device.Address,
            code,
            DateTimeOffset.Now.AddMinutes(5),
            GenerateTrustToken());
    }

    public void ConfirmPairing(SyncPairingSession session) {
        if (session.ExpiresAt < DateTimeOffset.Now) {
            return;
        }

        var devices = LoadPairedDevices()
            .Where(device => !string.Equals(device.DeviceId, session.DeviceId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        devices.Add(new PairedSyncDevice(
            session.DeviceId,
            session.DeviceName,
            session.Address,
            DateTimeOffset.Now,
            null,
            true,
            session.TrustToken));

        SavePairedDevices(devices);
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
}
