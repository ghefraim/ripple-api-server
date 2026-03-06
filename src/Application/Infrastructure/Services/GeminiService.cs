using System.Text;

using Application.Common.Interfaces;

using Ardalis.GuardClauses;

using Microsoft.Extensions.Configuration;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Application.Infrastructure.Services;

public class GeminiService(IConfiguration configuration) : IGeminiService
{
    private readonly GeminiConfiguration? _geminiSettings =
        configuration.GetSection("GeminiConfiguration").Get<GeminiConfiguration>();

    public async Task<string?> ImagePrompt(string prompt, string filePath, string? mimeType = null)
    {
        try
        {
            // Read the file as a byte array.
            byte[] fileBytes = File.ReadAllBytes(filePath);

            // Convert the byte array to a Base64 string.
            string base64String = Convert.ToBase64String(fileBytes);

            // Construct the JSON payload for the generateContent API.
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = mimeType ?? "image/png",
                                    data = base64String,
                                },
                            },
                        },
                    },
                },
            };

            var stringRequest = JsonConvert.SerializeObject(requestBody);

            return await PostAsync(stringRequest);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"JSON Payload Construct Error: {ex.Message}");
            return null;
        }
    }

    public async Task<string?> TextPrompt(string prompt)
    {
        try
        {
            // Construct the JSON payload for the generateContent API.
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = prompt } },
                    },
                },
            };

            var stringRequest = JsonConvert.SerializeObject(requestBody);

            return await PostAsync(stringRequest);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"JSON Payload Construct Error: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> PostAsync(string request)
    {
        try
        {
            Guard.Against.Null(_geminiSettings);

            using (var client = new HttpClient())
            {
                StringContent content = new StringContent(request, Encoding.UTF8, "application/json");

                var fullUrl = _geminiSettings.URL + _geminiSettings.Key;

                HttpResponseMessage response = await client.PostAsync(fullUrl, content);

                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();

                try
                {
                    JObject jsonResponse = JObject.Parse(responseBody);

                    string extractedText = (jsonResponse?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"] ?? " ").ToString();

                    return extractedText;
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"JSON Parse error: {ex.Message}\nResponse: {responseBody}");
                    return null;
                }
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP Request Error: {ex.Message}");
            return null;
        }
    }

    private class GeminiConfiguration
    {
        public required string URL { get; set; }
        public required string Key { get; set; }
    }
}
