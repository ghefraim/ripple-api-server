namespace Application.Common.Interfaces;

public interface IGeminiService
{
    Task<string?> ImagePrompt(string prompt, string filePath, string? mimeType = null);
    Task<string?> TextPrompt(string prompt);
}
