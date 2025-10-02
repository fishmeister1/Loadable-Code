using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Codeful.Models
{
    public class ChatMessage
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
        
        [JsonPropertyName("isUser")]
        public bool IsUser { get; set; }
        
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        [JsonPropertyName("thinkingProcess")]
        public string? ThinkingProcess { get; set; }
    }
    
    public class ChatData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [JsonPropertyName("title")]
        public string Title { get; set; } = "New Chat";
        
        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        [JsonPropertyName("lastMessageAt")]
        public DateTime LastMessageAt { get; set; } = DateTime.Now;
        
        [JsonIgnore]
        public string DisplayTitle => string.IsNullOrWhiteSpace(Title) || Title == "New Chat" 
            ? (Messages.Count > 0 && Messages[0].IsUser ? TruncateText(Messages[0].Content, 30) : "New Chat")
            : Title;
            
        [JsonIgnore]
        public string FormattedLastMessage => LastMessageAt.ToString("MMM dd, HH:mm");
        
        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength) + "...";
        }
    }
}