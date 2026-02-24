
namespace ToDo.RazorLib.Services;

public interface ISettingsService {
    void Set(string key, string value);
    string Get(string key, string defaultValue);
}
