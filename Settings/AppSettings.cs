using System.Text.Json;

namespace DiskSpaceAnalyzer.Settings
{
    public class AppSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DiskSpaceAnalyzer",
            "settings.json"
        );

        public long MinimumFileSize { get; set; } = 1024 * 1024; // 1MB
        public bool ShowHiddenFiles { get; set; } = false;
        public bool ShowSystemFiles { get; set; } = false;
        public string[] ExcludedExtensions { get; set; } = Array.Empty<string>();
        public string[] ExcludedFolders { get; set; } = Array.Empty<string>();
        public bool DarkMode { get; set; } = false;
        public bool AutoExpandNodes { get; set; } = false;
        public int MaxDisplayedItems { get; set; } = 1000;

        public void Save()
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(SettingsPath)!;
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string jsonString = JsonSerializer.Serialize(this, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(SettingsPath, jsonString);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string jsonString = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(jsonString);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }

            return new AppSettings();
        }
    }
}
