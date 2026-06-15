namespace ToDo.RazorLib.Services;

public interface ISyncSnapshotImportService {
    Task<SyncImportSummary> ImportNewAsync(
        SyncSnapshotResponse snapshot,
        SyncImportOptions? options = null,
        CancellationToken cancellationToken = default);
}
