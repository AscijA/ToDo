namespace ToDo.RazorLib.Services;

public interface ISyncService {
    SyncDeviceIdentity GetLocalDevice();
    void SaveLocalDevice(SyncDeviceIdentity identity);
    Task<SyncDiscoveryResult> DiscoverAvailableDevicesAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<PairedSyncDevice> GetPairedDevices();
    Task<SyncPairingSession?> StartPairingAsync(AvailableSyncDevice device, string localAddress, CancellationToken cancellationToken = default);
    Task<bool> ConfirmPairingAsync(SyncPairingSession session, string localAddress, CancellationToken cancellationToken = default);
    Task<PairedSyncDevice> PingPairedDeviceAsync(PairedSyncDevice device, CancellationToken cancellationToken = default);
    void RemovePairedDevice(string deviceId);
}
