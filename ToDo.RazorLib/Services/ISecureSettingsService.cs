namespace ToDo.RazorLib.Services;

public interface ISecureSettingsService {
    void Set(string key, string value);
    string Get(string key, string defaultValue);
    void Remove(string key);
}
