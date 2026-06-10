namespace ToDo.RazorLib.Services;

public sealed class AppVersionService : IAppVersionService {
    public AppVersionService(string version) {
        Version = string.IsNullOrWhiteSpace(version) ? "Unknown" : version;
    }

    public string Version { get; }
}
