using System.Text.Json;
using StatusLightChecker.Core.Colors;
using System.IO;
using System;

namespace StatusLightChecker.Core.Configuration;

public class ApplicationSettings
{
    public string ComPort { get; set; } = "COM3";
    public int BaudRate { get; set; } = 115200;
    public string SelectedApp { get; set; } = "MicrosoftTeams";
    public bool AutoStart { get; set; } = false;
    public StatusColors StatusColors { get; set; } = new StatusColors();
    public bool ShowTrayIcon { get; set; } = true;

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StatusLightChecker",
        "settings.json");

    public static ApplicationSettings? Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return null;

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<ApplicationSettings>(json);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to load settings");
            return null;
        }
    }

    public static void Save(ApplicationSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory!);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to save settings");
        }
    }

    private static void LogError(Exception ex, string message)
    {
        // Silent fail - logger may not be initialized yet
        Console.Error.WriteLine($"{message}: {ex.Message}");
    }
}
