using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using StrongLocker.Models;

namespace StrongLocker.Services;

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON under %AppData%\StrongLocker.</summary>
public sealed class SettingsStore
{
    public static string Dir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StrongLocker");

    public static string FilePath { get; } = Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                var s = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (s is not null) return s;
            }
        }
        catch
        {
            // Corrupt/unreadable settings fall back to defaults rather than crashing.
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Dir);
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        // Write-then-replace so a crash mid-write cannot truncate the real file.
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Copy(tmp, FilePath, overwrite: true);
        File.Delete(tmp);
    }
}
