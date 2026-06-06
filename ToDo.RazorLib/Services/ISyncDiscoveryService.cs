namespace ToDo.RazorLib.Services;

public interface ISyncDiscoveryService {
    Task<SyncDiscoveryResult> DiscoverAsync(SyncDeviceIdentity localDevice, CancellationToken cancellationToken = default);
}
