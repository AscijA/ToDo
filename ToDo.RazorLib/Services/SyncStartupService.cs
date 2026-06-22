namespace ToDo.RazorLib.Services;

public sealed class SyncStartupService {
    private readonly ISyncService syncService;
    private readonly ISyncTransportService syncTransportService;
    private readonly SyncActivityLogService activityLog;
    private readonly SemaphoreSlim startupLock = new(1, 1);

    public SyncStartupService(
        ISyncService syncService,
        ISyncTransportService syncTransportService,
        SyncActivityLogService activityLog) {
        this.syncService = syncService;
        this.syncTransportService = syncTransportService;
        this.activityLog = activityLog;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default) {
        await startupLock.WaitAsync(cancellationToken);
        try {
            var identity = syncService.GetLocalDevice();
            if (!identity.SyncEnabled) {
                return;
            }

            try {
                await syncTransportService.StartAsync(identity, cancellationToken);
                var status = syncTransportService.GetStatus();
                var address = status.Address ?? "this device";
                activityLog.AddSuccess(identity.DeviceName, $"Sync is ready at {address}.");
            }
            catch (Exception ex) {
                activityLog.AddError(identity.DeviceName, SyncUserMessages.Explain(ex));
            }
        }
        finally {
            startupLock.Release();
        }
    }
}
