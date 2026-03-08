using System.Text;
using System.Text.Json;

using Application.Common.Interfaces;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Infrastructure.Services;

public class GeminiLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly ILogger<GeminiLlmProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public GeminiLlmProvider(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<GeminiLlmProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("GeminiLlm");
        _logger = logger;

        var geminiConfig = configuration.GetSection("GeminiConfiguration");
        _apiKey = configuration["GeminiApiKey"]
            ?? Environment.GetEnvironmentVariable("GeminiApiKey")
            ?? geminiConfig["Key"]
            ?? string.Empty;

        _baseUrl = geminiConfig["URL"]
            ?? "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=";
    }

    public async Task<ActionPlanResult> GenerateActionPlanAsync(CascadeContext context, CancellationToken cancellationToken = default)
    {
        var prompt = BuildActionPlanPrompt(context);
        var responseText = await CallGeminiAsync(prompt, cancellationToken);

        if (responseText == null)
        {
            return new ActionPlanResult("LLM call failed", []);
        }

        var actions = ParseActionPlanResponse(responseText);
        return new ActionPlanResult(responseText, actions);
    }

    public async Task<RuleParseResult> ParseRuleAsync(string naturalLanguageInput, CancellationToken cancellationToken = default)
    {
        var prompt = BuildRuleParsePrompt(naturalLanguageInput);
        var responseText = await CallGeminiAsync(prompt, cancellationToken);

        if (responseText == null)
        {
            return new RuleParseResult(false, null, "LLM call failed — no response received.");
        }

        var ruleJson = ExtractJsonBlock(responseText);
        if (ruleJson == null)
        {
            return new RuleParseResult(false, null, "LLM response did not contain valid rule JSON.");
        }

        return new RuleParseResult(true, ruleJson, null);
    }

    private static string BuildActionPlanPrompt(CascadeContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an airport operations advisor. A disruption has been reported and cascade impacts have been computed.");
        sb.AppendLine("Generate a prioritized action plan. Be professional and concise.");
        sb.AppendLine("Be EXTREMELY concise. Each action description must be max 15 words in imperative mood (e.g., \"Reassign W6-2902 to gate B1\"). Each reasoning must be max 1 sentence.");
        sb.AppendLine();
        sb.AppendLine("## Disruption");
        sb.AppendLine($"- Flight: {context.DisruptedFlight.FlightNumber} ({context.DisruptedFlight.Airline})");
        sb.AppendLine($"- Route: {context.DisruptedFlight.Origin} -> {context.DisruptedFlight.Destination}");
        sb.AppendLine($"- Direction: {context.DisruptedFlight.Direction}");
        sb.AppendLine($"- Gate: {context.DisruptedFlight.GateCode ?? "unassigned"}");
        sb.AppendLine($"- Crew: {context.DisruptedFlight.CrewName ?? "unassigned"}");
        sb.AppendLine($"- Scheduled: {context.DisruptedFlight.ScheduledTime:HH:mm}");
        sb.AppendLine($"- Estimated: {context.DisruptedFlight.EstimatedTime?.ToString("HH:mm") ?? "N/A"}");
        sb.AppendLine($"- Disruption type: {context.DisruptionType}");
        sb.AppendLine($"- Details: {context.DisruptionDetails}");
        sb.AppendLine();

        if (context.CascadeImpacts.Count > 0)
        {
            sb.AppendLine("## Cascade Impacts");
            foreach (var impact in context.CascadeImpacts)
            {
                sb.AppendLine($"- [{impact.Severity}] {impact.ImpactType}: Flight {impact.AffectedFlightNumber} — {impact.Details}");
            }

            sb.AppendLine();
        }

        if (context.AvailableGates.Count > 0)
        {
            sb.AppendLine("## Available Gates");
            foreach (var gate in context.AvailableGates)
            {
                sb.AppendLine($"- {gate.Code} ({gate.Type}, {gate.SizeCategory}) — Active: {gate.IsActive}");
            }

            sb.AppendLine();
        }

        if (context.AvailableCrews.Count > 0)
        {
            sb.AppendLine("## Available Crews");
            foreach (var crew in context.AvailableCrews)
            {
                sb.AppendLine($"- {crew.Name} ({crew.Status}) — Shift: {crew.ShiftStart:HH:mm}-{crew.ShiftEnd:HH:mm}");
            }

            sb.AppendLine();
        }

        if (context.RuleRecommendations.Count > 0)
        {
            sb.AppendLine("## Rule Recommendations");
            foreach (var rec in context.RuleRecommendations)
            {
                sb.AppendLine($"- {rec}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("## Required Output Format");
        sb.AppendLine("Respond ONLY with a JSON array of actions. Each action object must have:");
        sb.AppendLine("- \"priority\" (integer, 1 = most urgent)");
        sb.AppendLine("- \"description\" (string, what to do — max 15 words, imperative mood)");
        sb.AppendLine("- \"reasoning\" (string, why — max 1 sentence)");
        sb.AppendLine("- \"suggestedAssignee\" (string or null, who should do it)");
        sb.AppendLine("- \"executionType\" (string: \"parallel\" if can run simultaneously with other actions, \"sequential\" if must wait)");
        sb.AppendLine("- \"dependsOn\" (array of priority numbers this action depends on, empty array [] if independent)");
        sb.AppendLine("- \"timeTarget\" (string: \"Immediate\", \"Within 15 min\", or \"Before departure\")");
        sb.AppendLine("- \"status\" (string: always \"pending\" for new plans)");
        sb.AppendLine();
        sb.AppendLine("Example:");
        sb.AppendLine("```json");
        sb.AppendLine("[{\"priority\":1,\"description\":\"Reassign W6-2902 to gate B1\",\"reasoning\":\"Gate A3 conflict from delayed arrival\",\"suggestedAssignee\":\"Gate Operations\",\"executionType\":\"parallel\",\"dependsOn\":[],\"timeTarget\":\"Immediate\",\"status\":\"pending\"},{\"priority\":2,\"description\":\"Notify W6-2902 crew of gate change\",\"reasoning\":\"Crew must relocate to new gate\",\"suggestedAssignee\":\"Crew Coordinator\",\"executionType\":\"sequential\",\"dependsOn\":[1],\"timeTarget\":\"Within 15 min\",\"status\":\"pending\"}]");
        sb.AppendLine("```");

        return sb.ToString();
    }

    private static string BuildRuleParsePrompt(string naturalLanguageInput)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an airport operations rule parser. Convert the following natural language rule into structured JSON.");
        sb.AppendLine();
        sb.AppendLine("## Rule JSON Schema");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"conditions\": [{\"field\": \"<field>\", \"operator\": \"<operator>\", \"value\": \"<value>\"}],");
        sb.AppendLine("  \"actions\": [{\"type\": \"<action_type>\", \"value\": \"<value>\"}]");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Supported fields: turnaround_minutes, delay_minutes, flight_type, gate_type, crew_status, flight_status, time_until_departure");
        sb.AppendLine("Supported operators: equals, not_equals, less_than, greater_than, in, not_in");
        sb.AppendLine("Supported action types: flag_severity (value: critical/warning/info), recommend (value: text), auto_notify (value: role name)");
        sb.AppendLine();
        sb.AppendLine("## Examples");
        sb.AppendLine("Input: \"If turnaround time is less than 35 minutes, flag as critical\"");
        sb.AppendLine("```json");
        sb.AppendLine("{\"conditions\":[{\"field\":\"turnaround_minutes\",\"operator\":\"less_than\",\"value\":\"35\"}],\"actions\":[{\"type\":\"flag_severity\",\"value\":\"critical\"}]}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Input: \"If delay is more than 60 minutes, notify duty manager and flag critical\"");
        sb.AppendLine("```json");
        sb.AppendLine("{\"conditions\":[{\"field\":\"delay_minutes\",\"operator\":\"greater_than\",\"value\":\"60\"}],\"actions\":[{\"type\":\"flag_severity\",\"value\":\"critical\"},{\"type\":\"auto_notify\",\"value\":\"duty_manager\"}]}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Input: \"When crew is assigned and departure is in less than 30 minutes, recommend crew reallocation\"");
        sb.AppendLine("```json");
        sb.AppendLine("{\"conditions\":[{\"field\":\"crew_status\",\"operator\":\"equals\",\"value\":\"assigned\"},{\"field\":\"time_until_departure\",\"operator\":\"less_than\",\"value\":\"30\"}],\"actions\":[{\"type\":\"recommend\",\"value\":\"crew_reallocation\"}]}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine($"Now parse this rule: \"{naturalLanguageInput}\"");
        sb.AppendLine();
        sb.AppendLine("Respond ONLY with the JSON object. No explanation.");

        return sb.ToString();
    }

    private List<ActionPlanAction> ParseActionPlanResponse(string responseText)
    {
        try
        {
            var jsonBlock = ExtractJsonBlock(responseText) ?? responseText.Trim();
            var actions = JsonSerializer.Deserialize<List<ActionPlanAction>>(jsonBlock, JsonOptions);
            return actions ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse action plan JSON from LLM response");
            return [];
        }
    }

    private static string? ExtractJsonBlock(string text)
    {
        var trimmed = text.Trim();

        var jsonStart = trimmed.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (jsonStart >= 0)
        {
            var contentStart = trimmed.IndexOf('\n', jsonStart);
            if (contentStart >= 0)
            {
                var jsonEnd = trimmed.IndexOf("```", contentStart, StringComparison.Ordinal);
                if (jsonEnd >= 0)
                {
                    return trimmed[(contentStart + 1)..jsonEnd].Trim();
                }
            }
        }

        if ((trimmed.StartsWith('[') && trimmed.EndsWith(']')) ||
            (trimmed.StartsWith('{') && trimmed.EndsWith('}')))
        {
            return trimmed;
        }

        return null;
    }

    private async Task<string?> CallGeminiAsync(string prompt, CancellationToken cancellationToken)
    {
        try
        {
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

            var json = JsonSerializer.Serialize(requestBody, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = _baseUrl + _apiKey;

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(responseBody);
            var extractedText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return extractedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini API call failed");
            return null;
        }
    }
}
