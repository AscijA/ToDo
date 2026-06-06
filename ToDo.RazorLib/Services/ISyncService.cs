namespace ToDo.RazorLib.Services;

public interface ISyncService {
    SyncDeviceIdentity GetLocalDevice();
    void SaveLocalDevice(SyncDeviceIdentity identity);
    Task<SyncDiscoveryResult> DiscoverAvailableDevicesAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<PairedSyncDevice> GetPairedDevices();
    SyncPairingSession StartPairing(AvailableSyncDevice device);
    void ConfirmPairing(SyncPairingSession session);
    void RemovePairedDevice(string deviceId);
}
