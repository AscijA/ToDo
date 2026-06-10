using Microsoft.Extensions.Logging;
using ToDo.Application.Interfaces.Services;

namespace ToDo.RazorLib.Services;

public sealed class AutoSyncChangeNotifier : IDataChangeNotifier, IDisposable {
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(2);

    private readonly ISyncService syncService;
    private readonly SyncChangeTracker changeTracker;
    private readonly ILogger<AutoSyncChangeNotifier>? logger;
    private readonly object gate = new();
    private readonly SemaphoreSlim syncLock = new(1, 1);
    private CancellationTokenSource? pendingSync;
    private bool disposed;

    public AutoSyncChangeNotifier(
        ISyncService syncService,
        SyncChangeTracker changeTracker,
        ILogger<AutoSyncChangeNotifier>? logger = null) {
        this.syncService = syncService;
        this.changeTracker = changeTracker;
        this.logger = logger;
    }

    public void NotifyChanged() {
        if (disposed) {
            return;
        }

        changeTracker.MarkLocalChange();

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
                try {
                    await syncService.PushLocalNewAsync(device, cancellationToken);
                    await syncService.ImportRemoteNewAsync(device, cancellationToken);
                }
                catch (Exception ex) {
                    logger?.LogWarning(ex, "Auto sync from paired device {DeviceName} failed.", device.DeviceName);
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
