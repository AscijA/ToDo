using ToDo.RazorLib.Services;

namespace ToDo.Maui.Android.Services;

public sealed class AndroidSettingsService : ISettingsService {
    public string Get(string key, string defaultValue) {
        return Preferences.Default.Get(key, defaultValue);
    }

    public void Set(string key, string value) {
        Preferences.Default.Set(key, value);
    }
}
