namespace ToDo.RazorLib.Services;

public sealed record StartupSettingsState(
    bool IsSupported,
    bool IsEnabled,
    bool IsDisabledByUser,
    bool IsDisabledByPolicy);

public interface IStartupSettingsService {
    Task<StartupSettingsState> GetStateAsync();
    Task<StartupSettingsState> SetEnabledAsync(bool enabled);
}
