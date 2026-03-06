using System.Text.Json;

using Application.Common.Models.Audit;
using Application.Domain.Common;

namespace Application.Domain.Entities;

/// <summary>
/// Entity for storing audit trail records.
/// </summary>
public class AuditEntry : BaseEntity, IOrganizationScoped
{
    /// <summary>
    /// Organization this audit entry belongs to.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Name of the entity that was modified.
    /// </summary>
    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Type of action performed (Added, Modified, Deleted).
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the action occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// ID of the user who performed the action.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Email of the user who performed the action.
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// JSON serialized property changes (before/after values).
    /// </summary>
    public string? DetailsJson { get; set; }

    /// <summary>
    /// Deserialize the property changes from JSON.
    /// </summary>
    /// <returns>List of property change details</returns>
    public List<AuditDetailInfo> GetDetails()
    {
        if (string.IsNullOrEmpty(DetailsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<AuditDetailInfo>>(DetailsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Serialize property changes to JSON.
    /// </summary>
    /// <param name="details">List of property change details</param>
    public void SetDetails(List<AuditDetailInfo> details)
    {
        DetailsJson = details.Count > 0 ? JsonSerializer.Serialize(details) : null;
    }
}