using ToDo.RazorLib.Services;

namespace ToDo.Maui.Windows.Services;

public class MauiSettingsService : ISettingsService {
    private const int InlineValueLimit = 4096;
    private const string FileValuePrefix = "file:";

    public void Set(string key, string value) {
        if (value.Length > InlineValueLimit) {
            SetFileValue(key, value);
            return;
        }

        try {
            Preferences.Default.Set(key, value);
            DeleteFileValue(key);
        }
        catch (System.Runtime.InteropServices.COMException) {
            SetFileValue(key, value);
        }
    }

    public string Get(string key, string defaultValue) {
        var value = Preferences.Default.Get(key, defaultValue);
        if (value.StartsWith(FileValuePrefix, StringComparison.Ordinal)) {
            var path = GetFilePathFromMarker(value);
            return File.Exists(path) ? File.ReadAllText(path) : defaultValue;
        }

        return value;
    }

    private static void SetFileValue(string key, string value) {
        var path = GetFilePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, value);
        Preferences.Default.Set(key, FileValuePrefix + Path.GetFileName(path));
    }

    private static void DeleteFileValue(string key) {
        var path = GetFilePath(key);
        if (File.Exists(path)) {
            File.Delete(path);
        }
    }

    private static string GetFilePathFromMarker(string marker) {
        return Path.Combine(GetSettingsDirectory(), marker[FileValuePrefix.Length..]);
    }

    private static string GetFilePath(string key) {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
        return Path.Combine(GetSettingsDirectory(), $"{Convert.ToHexString(bytes)}.txt");
    }

    private static string GetSettingsDirectory() {
        return Path.Combine(FileSystem.AppDataDirectory, "settings");
    }
}
