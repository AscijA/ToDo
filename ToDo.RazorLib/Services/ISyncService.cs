namespace ToDo.RazorLib.Services;

public interface ISyncService {
    SyncDeviceIdentity GetLocalDevice();
    void SaveLocalDevice(SyncDeviceIdentity identity);
    Task<SyncDiscoveryResult> DiscoverAvailableDevicesAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<PairedSyncDevice> GetPairedDevices();
    Task<SyncPairingSession?> StartPairingAsync(AvailableSyncDevice device, string localAddress, CancellationToken cancellationToken = default);
    Task<bool> ConfirmPairingAsync(SyncPairingSession session, string localAddress, CancellationToken cancellationToken = default);
    Task<PairedSyncDevice> PingPairedDeviceAsync(PairedSyncDevice device, CancellationToken cancellationToken = default);
    Task<SyncSnapshotResponse> FetchSnapshotAsync(PairedSyncDevice device, CancellationToken cancellationToken = default);
    Task<SyncPreviewSummary> PreviewSyncAsync(PairedSyncDevice device, CancellationToken cancellationToken = default);
    Task<SyncImportSummary> ImportRemoteNewAsync(
        PairedSyncDevice device,
        SyncImportOptions? options = null,
        CancellationToken cancellationToken = default);
    Task<SyncImportSummary> PushLocalNewAsync(PairedSyncDevice device, CancellationToken cancellationToken = default);
    void RemovePairedDevice(string deviceId);
}
