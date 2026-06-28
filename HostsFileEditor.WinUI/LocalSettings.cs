using System.Text.Json;

namespace HostsFileEditor;

internal static class LocalSettings
{
    private static readonly string _settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HostsFileEditor");
    private static readonly string _settingsPath = Path.Combine(_settingsDirectory, "settings.json");

    private static readonly Lock _lock = new();
    private static readonly Dictionary<string, bool> _cache = Load();

    public static bool GetBool(string key, bool defaultValue)
    {
        lock (_lock)
        {
            return _cache.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }

    public static void SetBool(string key, bool value)
    {
        lock (_lock)
        {
            _cache[key] = value;
            Save();
        }
    }

    private static Dictionary<string, bool> Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var dict = JsonSerializer.Deserialize(json, LocalSettingsJsonContext.Default.DictionaryStringBoolean);
                return dict ?? [];
            }
        }
        catch
        {
            // ignore and recreate
        }

        return [];
    }

    private static void Save()
    {
        try
        {
            if (!Directory.Exists(_settingsDirectory))
            {
                Directory.CreateDirectory(_settingsDirectory);
            }

            var json = JsonSerializer.Serialize(_cache, LocalSettingsJsonContext.Default.DictionaryStringBoolean);
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // ignore persistence errors
        }
    }
}
