namespace Application.Common.Exceptions;

public class PaymentRequiredException : Exception
{
    private static readonly Dictionary<string, string> FeatureDisplayNames = new()
    {
        ["maxMembers"] = "Team Members",
        ["maxTodoLists"] = "Todo Lists",
        ["canExport"] = "Export",
        ["apiAccess"] = "API Access",
    };

    public string? FeatureKey { get; }
    public string? FeatureDisplayName { get; }
    public int? Limit { get; }
    public int? CurrentUsage { get; }

    public PaymentRequiredException()
        : base("Upgrade required to access this feature.") { }

    public PaymentRequiredException(string? message)
        : base(message) { }

    public PaymentRequiredException(string? message, string? featureKey, int? limit = null, int? currentUsage = null)
        : base(message)
    {
        FeatureKey = featureKey;
        FeatureDisplayName = GetDisplayName(featureKey);
        Limit = limit;
        CurrentUsage = currentUsage;
    }

    public PaymentRequiredException(string? message, Exception? innerException)
        : base(message, innerException) { }

    public static string GetDisplayName(string? featureKey)
    {
        if (string.IsNullOrEmpty(featureKey))
        {
            return "this feature";
        }

        return FeatureDisplayNames.TryGetValue(featureKey, out var displayName)
            ? displayName
            : FormatFeatureKey(featureKey);
    }

    private static string FormatFeatureKey(string featureKey)
    {
        // Convert camelCase to Title Case (e.g., "maxTodoLists" -> "Max Todo Lists")
        var result = System.Text.RegularExpressions.Regex.Replace(
            featureKey,
            "([a-z])([A-Z])",
            "$1 $2");

        return char.ToUpper(result[0]) + result[1..];
    }
}
