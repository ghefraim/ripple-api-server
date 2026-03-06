namespace Application.Common.Models.Audit;

/// <summary>
/// DTO for property change information in audit records
/// </summary>
public class AuditDetailInfo
{
    /// <summary>
    /// Name of the property that was changed
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Original value of the property (JSON serialized)
    /// </summary>
    public string? OriginalValue { get; set; }

    /// <summary>
    /// New value of the property (JSON serialized)
    /// </summary>
    public string? NewValue { get; set; }
}