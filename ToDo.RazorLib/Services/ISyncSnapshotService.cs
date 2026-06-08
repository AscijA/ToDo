namespace ToDo.RazorLib.Services;

public interface ISyncSnapshotService {
    Task<SyncSnapshotResponse> CreateSnapshotAsync(SyncDeviceIdentity identity, CancellationToken cancellationToken = default);
}
