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

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                if (!Directory.Exists(SettingDir))
                    Directory.CreateDirectory(SettingDir);

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        public static AppSettings LoadSettings()
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
