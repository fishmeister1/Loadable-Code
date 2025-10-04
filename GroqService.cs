using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Codeful.Services
{
    public class AiResponse
    {
        public string ThinkingProcess { get; set; } = "";
        public string Conclusion { get; set; } = "";
    }

    public class GroqService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string ApiUrl = "https://api.groq.com/openai/v1/chat/completions";

        public GroqService()
        {
            _httpClient = new HttpClient();
            _apiKey = LoadApiKey();
            
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Codeful/1.0");
        }

        private string LoadApiKey()
        {
            try
            {
                var envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
                if (!File.Exists(envPath))
                {
                    envPath = ".env";
                }

                var lines = File.ReadAllLines(envPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("GROQ-API-KEY="))
                    {
                        return line.Substring("GROQ-API-KEY=".Length).Trim();
                    }
                }
                throw new InvalidOperationException("GROQ-API-KEY not found in .env file");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load API key: {ex.Message}");
            }
        }

        public async Task<AiResponse> SendMessageAsync(string userMessage, Action<string>? onThinking = null)
        {
            try
            {
                onThinking?.Invoke("Thinking...");

                var request = new
                {
                    model = "qwen/qwen3-32b",
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = @"You are an expert coding agent with deep knowledge of programming languages, frameworks, and software development best practices. Please state your thought process in short detail and present your final conclusions at the end.

When responding, wrap your thought process in <think></think> tags, then provide your final answer outside the tags.

Who you are:
- You are Loadable Code, an AI coding assistant designed to help developers write, debug, and optimize code across various programming languages and frameworks.

Your expertise includes:
- Writing clean, efficient, and maintainable code
- Debugging and troubleshooting complex issues
- Code architecture and design patterns
- Performance optimization
- Security best practices
- Modern development workflows and tools

When responding:
- Use <think>your reasoning process here</think> for your internal thought process
- Provide clear, practical code solutions after the thinking
- Explain your reasoning and approach
- Include relevant comments in code examples
- Suggest improvements and alternatives when applicable
- Be concise but thorough in your explanations
- Focus on production-ready, scalable solutions
- If no reference to coding or programming is made, respond with a statement saying that you are not able to assist with that topic."
                        },
                        new
                        {
                            role = "user",
                            content = userMessage
                        }
                    },
                    max_tokens = 1500,
                    temperature = 0.3
                };

                onThinking?.Invoke("Working...");

                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(ApiUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"API request failed: {response.StatusCode} - {errorContent}");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(responseJson);
                
                if (document.RootElement.TryGetProperty("choices", out var choicesArray) && 
                    choicesArray.GetArrayLength() > 0)
                {
                    var firstChoice = choicesArray[0];
                    if (firstChoice.TryGetProperty("message", out var messageElement) &&
                        messageElement.TryGetProperty("content", out var contentElement))
                    {
                        var fullResponse = contentElement.GetString() ?? "No response received";
                        return ParseResponse(fullResponse);
                    }
                }

                return new AiResponse { Conclusion = "Unable to parse response" };
            }
            catch (Exception ex)
            {
                return new AiResponse { Conclusion = $"Error: {ex.Message}" };
            }
        }

        private AiResponse ParseResponse(string fullResponse)
        {
            var thinkStart = fullResponse.IndexOf("<think>");
            var thinkEnd = fullResponse.IndexOf("</think>");
            
            string thinkingText = "";
            string conclusion = "";
            
            if (thinkStart != -1 && thinkEnd != -1 && thinkEnd > thinkStart)
            {
                // Extract thinking text (but we won't display it)
                thinkingText = fullResponse.Substring(thinkStart + 7, thinkEnd - thinkStart - 7).Trim();
                
                // Extract conclusion (everything after thinking tags)
                var afterThink = fullResponse.Substring(thinkEnd + 8).Trim();
                var beforeThink = fullResponse.Substring(0, thinkStart).Trim();
                
                // Use content after think tags if available, otherwise use content before
                conclusion = !string.IsNullOrEmpty(afterThink) ? afterThink : beforeThink;
            }
            else
            {
                // No think tags found - use entire response as conclusion
                conclusion = fullResponse.Trim();
            }
            
            return new AiResponse 
            { 
                ThinkingProcess = thinkingText, 
                Conclusion = string.IsNullOrEmpty(conclusion) ? "No conclusion provided" : conclusion 
            };
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}