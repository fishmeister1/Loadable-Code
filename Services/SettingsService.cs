using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Codeful.Models;

namespace Codeful.Services
{
    public class SettingsService
    {
        private readonly string _dataDirectory;
        private readonly string _settingsFilePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public SettingsService()
        {
            // Store in user's AppData folder
            _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Codeful");
            _settingsFilePath = Path.Combine(_dataDirectory, "settings.json");
            
            // Ensure directory exists
            Directory.CreateDirectory(_dataDirectory);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<UserSettings> LoadSettingsAsync()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    // Create default settings if file doesn't exist
                    var defaultSettings = new UserSettings();
                    await SaveSettingsAsync(defaultSettings);
                    return defaultSettings;
                }

                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<UserSettings>(json, _jsonOptions);
                return settings ?? new UserSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                return new UserSettings();
            }
        }

        public async Task SaveSettingsAsync(UserSettings settings)
        {
            try
            {
                settings.LastModified = DateTime.Now;
                var json = JsonSerializer.Serialize(settings, _jsonOptions);
                await File.WriteAllTextAsync(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeleteSettingsAsync()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    File.Delete(_settingsFilePath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting settings: {ex.Message}");
                return false;
            }
        }
    }
}