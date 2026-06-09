namespace ToDo.RazorLib.Services;

public interface ISyncSnapshotImportService {
    Task<SyncImportSummary> ImportNewAsync(SyncSnapshotResponse snapshot, CancellationToken cancellationToken = default);
}
