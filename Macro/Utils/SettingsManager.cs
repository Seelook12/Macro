using System;
using System.IO;
using System.Text.Json;
using Macro.Models;

namespace Macro.Utils
{
    public class SettingsManager
    {
        private static readonly string SettingDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Setting");
        private static readonly string SettingFile = Path.Combine(SettingDir, "appsettings.json");
        private static readonly object _fileLock = new object();
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public static void SaveSettings(AppSettings settings)
        {
            lock (_fileLock)
            {
                try
                {
                    if (!Directory.Exists(SettingDir))
                        Directory.CreateDirectory(SettingDir);

                    var json = JsonSerializer.Serialize(settings, _jsonOptions);
                    File.WriteAllText(SettingFile, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
                }
            }
        }

        public static AppSettings LoadSettings()
        {
            lock (_fileLock)
            {
                try
                {
                    if (File.Exists(SettingFile))
                    {
                        var json = File.ReadAllText(SettingFile);
                        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
                }
                return new AppSettings();
            }
        }
    }
}
