namespace ToDo.RazorLib.Services;

public interface ISyncTransportService : IAsyncDisposable {
    SyncTransportStatus GetStatus();
    IReadOnlyList<IncomingPairingRequest> GetIncomingPairingRequests();
    void ApproveIncomingPairing(string sessionId);
    void RejectIncomingPairing(string sessionId);
    Task StartAsync(SyncDeviceIdentity identity, CancellationToken cancellationToken = default);
    Task StopAsync();
}
