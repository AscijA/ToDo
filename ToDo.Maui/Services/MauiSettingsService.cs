using ToDo.RazorLib.Services;
namespace ToDo.Maui.Services;

public class MauiSettingsService : ISettingsService {
    public void Set(string key, string value) => Preferences.Default.Set(key, value);
    public string Get(string key, string defaultValue) => Preferences.Default.Get(key, defaultValue);
}