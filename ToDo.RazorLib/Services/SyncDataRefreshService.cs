namespace ToDo.RazorLib.Services;

public sealed class SyncDataRefreshService {
    public event Action? RefreshRequested;

    public void NotifyChanged() {
        RefreshRequested?.Invoke();
    }
}
