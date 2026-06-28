using System.IO;
using System.Text.Json;
using NetSwitch.Models;

namespace NetSwitch.Services;

/// <summary>Loads and persists <see cref="AppSettings"/> as JSON under %AppData%\NetSwitch.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _dir;
    private readonly string _file;

    public SettingsService()
    {
        _dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetSwitch");
        _file = Path.Combine(_dir, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_file))
            {
                var json = File.ReadAllText(_file);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                    return settings;
            }
        }
        catch
        {
            // Corrupt/unreadable file → fall back to defaults.
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(_dir);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_file, json);
    }
}
