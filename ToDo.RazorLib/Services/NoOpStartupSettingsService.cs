namespace ToDo.RazorLib.Services;

public sealed class NoOpStartupSettingsService : IStartupSettingsService {
    private static readonly StartupSettingsState UnsupportedState = new(false, false, false, false);

    public Task<StartupSettingsState> GetStateAsync() => Task.FromResult(UnsupportedState);

    public Task<StartupSettingsState> SetEnabledAsync(bool enabled) => Task.FromResult(UnsupportedState);
}
