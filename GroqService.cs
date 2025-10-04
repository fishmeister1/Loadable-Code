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
- Do not use '#' for headers, instead stick to '**' for making them bold
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
            // Handle null response
            if (string.IsNullOrEmpty(fullResponse))
            {
                return new AiResponse { Conclusion = "No response received" };
            }
            
            // STEP 1: Remove the thought process text FIRST
            string cleanedResponse = RemoveThoughtProcess(fullResponse);
            
            // STEP 2: Return the cleaned information (no formatting yet)
            return new AiResponse 
            { 
                ThinkingProcess = "", // Always empty since we removed it
                Conclusion = string.IsNullOrEmpty(cleanedResponse) ? "No response provided" : cleanedResponse 
            };
        }
        
        private string RemoveThoughtProcess(string fullResponse)
        {
            var thinkStart = fullResponse.IndexOf("<think>");
            var thinkEnd = fullResponse.IndexOf("</think>");
            
            if (thinkStart != -1 && thinkEnd != -1 && thinkEnd > thinkStart)
            {
                // Extract only the content after thinking tags
                var afterThink = fullResponse.Substring(thinkEnd + 8).Trim();
                return afterThink;
            }
            else
            {
                // No think tags found - return entire response
                return fullResponse.Trim();
            }
        }


        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}