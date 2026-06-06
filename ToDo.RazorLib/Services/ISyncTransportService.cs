namespace ToDo.RazorLib.Services;

public interface ISyncTransportService : IAsyncDisposable {
    SyncTransportStatus GetStatus();
    Task StartAsync(SyncDeviceIdentity identity, CancellationToken cancellationToken = default);
    Task StopAsync();
}
