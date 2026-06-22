using ToDo.RazorLib.Services;
using Windows.ApplicationModel;

namespace ToDo.Maui.Windows.Services;

public sealed class WindowsStartupSettingsService : IStartupSettingsService {
    private const string StartupTaskId = "ToDoStartupTask";

    public async Task<StartupSettingsState> GetStateAsync() {
        try {
            var startupTask = await StartupTask.GetAsync(StartupTaskId);
            return MapState(startupTask.State);
        }
        catch {
            return new StartupSettingsState(false, false, false, false);
        }
    }

    public async Task<StartupSettingsState> SetEnabledAsync(bool enabled) {
        try {
            var startupTask = await StartupTask.GetAsync(StartupTaskId);

            if (enabled) {
                var state = await startupTask.RequestEnableAsync();
                return MapState(state);
            }

            startupTask.Disable();
            return MapState(startupTask.State);
        }
        catch {
            return new StartupSettingsState(false, false, false, false);
        }
    }

    private static StartupSettingsState MapState(StartupTaskState state) =>
        state switch {
            StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy => new(true, true, false, false),
            StartupTaskState.DisabledByUser => new(true, false, true, false),
            StartupTaskState.DisabledByPolicy => new(true, false, false, true),
            _ => new(true, false, false, false)
        };
}
