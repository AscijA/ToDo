using System.Text.Json;

namespace ToDo.RazorLib.Services;

public sealed class SyncActivityLogService {
    private const string LogKey = "Sync_ActivityLog";
    private const int MaxEntries = 30;
    private readonly ISettingsService settings;

    public event Action? Changed;

    public SyncActivityLogService(ISettingsService settings) {
        this.settings = settings;
    }

    public IReadOnlyList<SyncActivityLogEntry> GetEntries() {
        var json = settings.Get(LogKey, "");
        if (string.IsNullOrWhiteSpace(json)) {
            return [];
        }

        try {
            return JsonSerializer.Deserialize<List<SyncActivityLogEntry>>(json) ?? [];
        }
        catch (JsonException) {
            return [];
        }
    }

    public void AddSuccess(string deviceName, string message) {
        Add(new SyncActivityLogEntry(DateTimeOffset.Now, deviceName, message, SyncActivityStatus.Success));
    }

    public void AddWarning(string deviceName, string message) {
        Add(new SyncActivityLogEntry(DateTimeOffset.Now, deviceName, message, SyncActivityStatus.Warning));
    }

    public void AddError(string deviceName, string message) {
        Add(new SyncActivityLogEntry(DateTimeOffset.Now, deviceName, message, SyncActivityStatus.Error));
    }

    public void Clear() {
        settings.Set(LogKey, string.Empty);
        Changed?.Invoke();
    }

    private void Add(SyncActivityLogEntry entry) {
        var entries = GetEntries()
            .Prepend(entry)
            .Take(MaxEntries)
            .ToList();

        settings.Set(LogKey, JsonSerializer.Serialize(entries));
        Changed?.Invoke();
    }
}

public sealed record SyncActivityLogEntry(
    DateTimeOffset At,
    string DeviceName,
    string Message,
    SyncActivityStatus Status);

public enum SyncActivityStatus {
    Success,
    Warning,
    Error
}
