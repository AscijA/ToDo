using ToDo.RazorLib.Services;

namespace ToDo.Maui.Android.Services;

public sealed class AndroidSecureSettingsService : ISecureSettingsService {
    public string Get(string key, string defaultValue) {
        try {
            return SecureStorage.Default.GetAsync(key).GetAwaiter().GetResult() ?? defaultValue;
        }
        catch {
            return defaultValue;
        }
    }

    public void Set(string key, string value) {
        try {
            SecureStorage.Default.SetAsync(key, value).GetAwaiter().GetResult();
        }
        catch {
            Preferences.Default.Set(key, value);
        }
    }

    public void Remove(string key) {
        SecureStorage.Default.Remove(key);
        Preferences.Default.Remove(key);
    }
}
