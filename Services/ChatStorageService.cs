using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Codeful.Models;

namespace Codeful.Services
{
    public class ChatStorageService
    {
        private readonly string _dataDirectory;
        private readonly string _chatsDirectory;
        private readonly JsonSerializerOptions _jsonOptions;

        public ChatStorageService()
        {
            // Store in user's AppData folder
            _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Codeful");
            _chatsDirectory = Path.Combine(_dataDirectory, "Chats");
            
            // Ensure directories exist
            Directory.CreateDirectory(_dataDirectory);
            Directory.CreateDirectory(_chatsDirectory);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<List<ChatData>> LoadAllChatsAsync()
        {
            try
            {
                var chatFiles = Directory.GetFiles(_chatsDirectory, "*.json");
                var chats = new List<ChatData>();

                foreach (var file in chatFiles)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var chat = JsonSerializer.Deserialize<ChatData>(json, _jsonOptions);
                        if (chat != null)
                        {
                            chats.Add(chat);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue loading other chats
                        System.Diagnostics.Debug.WriteLine($"Error loading chat file {file}: {ex.Message}");
                    }
                }

                // Sort by last message time (newest first)
                return chats.OrderByDescending(c => c.LastMessageAt).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading chats: {ex.Message}");
                return new List<ChatData>();
            }
        }

        public async Task<ChatData> SaveChatAsync(ChatData chat)
        {
            try
            {
                // Update last message time
                if (chat.Messages.Count > 0)
                {
                    chat.LastMessageAt = chat.Messages.Max(m => m.Timestamp);
                }

                // Set title to first user message if not set
                if (string.IsNullOrWhiteSpace(chat.Title) || chat.Title == "New Chat")
                {
                    var firstUserMessage = chat.Messages.FirstOrDefault(m => m.IsUser);
                    if (firstUserMessage != null && !string.IsNullOrWhiteSpace(firstUserMessage.Content))
                    {
                        chat.Title = TruncateText(firstUserMessage.Content, 50);
                    }
                }

                var filePath = Path.Combine(_chatsDirectory, $"{chat.Id}.json");
                var json = JsonSerializer.Serialize(chat, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);
                
                return chat;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving chat: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeleteChatAsync(string chatId)
        {
            try
            {
                var filePath = Path.Combine(_chatsDirectory, $"{chatId}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting chat {chatId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteAllChatsAsync()
        {
            try
            {
                var chatFiles = Directory.GetFiles(_chatsDirectory, "*.json");
                foreach (var file in chatFiles)
                {
                    File.Delete(file);
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting all chats: {ex.Message}");
                return false;
            }
        }

        public async Task<ChatData?> LoadChatAsync(string chatId)
        {
            try
            {
                var filePath = Path.Combine(_chatsDirectory, $"{chatId}.json");
                if (!File.Exists(filePath))
                    return null;

                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<ChatData>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading chat {chatId}: {ex.Message}");
                return null;
            }
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength).Trim() + "...";
        }
    }
}