using Microsoft.Extensions.Logging;
using ToDo.Application.Interfaces.Services;

namespace ToDo.RazorLib.Services;

public sealed class AutoSyncChangeNotifier : IDataChangeNotifier, IDisposable {
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(2);

    private readonly ISyncService syncService;
    private readonly SyncChangeTracker changeTracker;
    private readonly SyncActivityLogService activityLog;
    private readonly ILogger<AutoSyncChangeNotifier>? logger;
    private readonly object gate = new();
    private readonly SemaphoreSlim syncLock = new(1, 1);
    private CancellationTokenSource? pendingSync;
    private bool disposed;

    public AutoSyncChangeNotifier(
        ISyncService syncService,
        SyncChangeTracker changeTracker,
        SyncActivityLogService activityLog,
        ILogger<AutoSyncChangeNotifier>? logger = null) {
        this.syncService = syncService;
        this.changeTracker = changeTracker;
        this.activityLog = activityLog;
        this.logger = logger;
    }

    public void NotifyChanged(params Guid[] entityIds) {
        if (disposed) {
            return;
        }

        changeTracker.MarkLocalChange(entityIds);
        ScheduleSyncIfEnabled();
    }

    public void NotifyDeleted(params Guid[] entityIds) {
        if (disposed) {
            return;
        }

        changeTracker.MarkDeleted(entityIds);
        ScheduleSyncIfEnabled();
    }

    private void ScheduleSyncIfEnabled() {
        if (disposed) {
            return;
        }

        var identity = syncService.GetLocalDevice();
        if (!identity.SyncEnabled || !identity.AutoSyncOnChanges || syncService.GetPairedDevices().Count == 0) {
            return;
        }

        CancellationTokenSource syncRequest;
        lock (gate) {
            pendingSync?.Cancel();
            pendingSync?.Dispose();
            pendingSync = new CancellationTokenSource();
            syncRequest = pendingSync;
        }

        _ = RunDebouncedSyncAsync(syncRequest.Token);
    }

    private async Task RunDebouncedSyncAsync(CancellationToken cancellationToken) {
        try {
            await Task.Delay(DebounceDelay, cancellationToken);
            await SyncPairedDevicesAsync(cancellationToken);
        }
        catch (OperationCanceledException) {
        }
        catch (Exception ex) {
            logger?.LogWarning(ex, "Auto sync failed.");
        }
    }

    private async Task SyncPairedDevicesAsync(CancellationToken cancellationToken) {
        await syncLock.WaitAsync(cancellationToken);
        try {
            var identity = syncService.GetLocalDevice();
            if (!identity.SyncEnabled || !identity.AutoSyncOnChanges) {
                return;
            }

            foreach (var device in syncService.GetPairedDevices()) {
                cancellationToken.ThrowIfCancellationRequested();
                if (device.NextRetryAt is { } nextRetryAt && nextRetryAt > DateTimeOffset.Now) {
                    activityLog.AddWarning(device.DeviceName, $"Auto sync will retry after {nextRetryAt.ToLocalTime():HH:mm}.");
                    continue;
                }

                try {
                    var push = await syncService.PushLocalNewAsync(device, cancellationToken);
                    var import = await syncService.ImportRemoteNewAsync(device, cancellationToken: cancellationToken);
                    var skipped = push.SkippedCount + import.SkippedCount;
                    var skippedText = skipped == 0
                        ? string.Empty
                        : $" {skipped} change(s) were already up to date or kept because this device had the newer copy.";

                    activityLog.AddSuccess(device.DeviceName, $"Auto sync finished. Sent {push.ImportedCount} change(s), received {import.ImportedCount} change(s).{skippedText}");
                }
                catch (Exception ex) {
                    logger?.LogWarning(ex, "Auto sync from paired device {DeviceName} failed.", device.DeviceName);
                    activityLog.AddError(device.DeviceName, SyncUserMessages.Explain(ex));
                }
            }
        }
        finally {
            syncLock.Release();
        }
    }

    public void Dispose() {
        disposed = true;
        lock (gate) {
            pendingSync?.Cancel();
            pendingSync?.Dispose();
            pendingSync = null;
        }

        syncLock.Dispose();
    }
}
