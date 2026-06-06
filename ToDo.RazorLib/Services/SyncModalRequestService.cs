namespace ToDo.RazorLib.Services;

public sealed class SyncModalRequestService {
    public event Action? OpenRequested;

    public void RequestOpen() {
        OpenRequested?.Invoke();
    }
}
