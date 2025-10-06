using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Controls;

namespace ReMux2
{
    // Simple service locator for app-wide utilities
    public static class AppServices
    {
        public static SettingsService Settings { get; } = new SettingsService();
        public static FfmpegService Ffmpeg { get; } = new FfmpegService();
    }

    // JSON persistence under LocalAppData
    public class SettingsService
    {
        private string RootDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReMux2");
        private string FilePath => Path.Combine(RootDir, "user_settings.json");

        public T Get<T>(string key, T defaultValue)
        {
            try
            {
                if (!File.Exists(FilePath)) return defaultValue;
                var json = File.ReadAllText(FilePath);
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
                if (dict != null && dict.TryGetValue(key, out var val) && val is not null)
                {
                    try
                    {
                        var elem = (System.Text.Json.JsonElement)val;
                        var t = elem.Deserialize<T>();
                        if (t != null) return t;
                    }
                    catch { }
                    try { return (T)Convert.ChangeType(val, typeof(T)); } catch { }
                }
            }
            catch { }
            return defaultValue;
        }

        public void Set<T>(string key, T value)
        {
            var settings = LoadSettings();
            settings[key] = value;

            try
            {
                Directory.CreateDirectory(RootDir);
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                // Broad catch to prevent app crash on config write failure
                Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        private Dictionary<string, object?> LoadSettings()
        {
            try
            {
                if (!File.Exists(FilePath)) return new Dictionary<string, object?>();
                var json = File.ReadAllText(FilePath);
                return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new Dictionary<string, object?>();
            }
            catch
            {
                return new Dictionary<string, object?>();
            }
        }
    }

    // ETA formatting helper
    public class ProgressUtils
    {
        public string FormatEta(TimeSpan elapsed, double percent)
        {
            if (percent <= 0) return "ETA: --:--:--";
            if (percent >= 1.0) return "ETA: 00:00:00";
            long etaTicks = (long)(elapsed.Ticks / percent) - elapsed.Ticks;
            var eta = TimeSpan.FromTicks(etaTicks);
            return $"ETA: {eta:hh\\:mm\\:ss}";
        }
    }


}