using System;
using System.Text.Json.Serialization;

namespace Codeful.Models
{
    public class UserSettings
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("notifications")]
        public bool Notifications { get; set; } = true;
        
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";
        
        [JsonPropertyName("lastModified")]
        public DateTime LastModified { get; set; } = DateTime.Now;
        
        // Add more settings here in the future
        // e.g., Theme, Language, etc.
    }
}