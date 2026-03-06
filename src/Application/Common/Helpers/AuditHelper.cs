using Application.Common.Attributes;
using Application.Common.Models.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Reflection;
using System.Text.Json;

namespace Application.Common.Helpers;

/// <summary>
/// Static utility class for audit processing
/// </summary>
public static class AuditHelper
{
    /// <summary>
    /// Serialize property value to JSON
    /// </summary>
    /// <param name="value">The value to serialize</param>
    /// <returns>JSON serialized value or null</returns>
    public static string? SerializePropertyValue(object? value)
    {
        if (value == null)
            return null;

        try
        {
            // Handle common types that don't need JSON serialization
            if (value is string str)
                return str;

            if (value.GetType().IsPrimitive || value is DateTime || value is DateTimeOffset || value is decimal)
                return value.ToString();

            // For complex types, use JSON serialization
            return JsonSerializer.Serialize(value);
        }
        catch
        {
            return value.ToString();
        }
    }

    /// <summary>
    /// Extract property changes from EntityEntry
    /// </summary>
    /// <param name="entry">The entity entry to analyze</param>
    /// <returns>List of changed properties with before/after values</returns>
    public static List<AuditDetailInfo> GetChangedProperties(EntityEntry entry)
    {
        var changes = new List<AuditDetailInfo>();

        foreach (var property in entry.Properties)
        {
            // Skip properties excluded from audit
            if (HasExcludeFromAuditAttribute(entry.Entity.GetType(), property.Metadata.Name))
                continue;

            // For Modified entities, check if the property actually changed
            if (entry.State == EntityState.Modified && !property.IsModified)
                continue;

            var change = new AuditDetailInfo
            {
                PropertyName = property.Metadata.Name
            };

            // Set original and new values based on entity state
            switch (entry.State)
            {
                case EntityState.Added:
                    change.OriginalValue = null;
                    change.NewValue = SerializePropertyValue(property.CurrentValue);
                    break;

                case EntityState.Deleted:
                    change.OriginalValue = SerializePropertyValue(property.OriginalValue);
                    change.NewValue = null;
                    break;

                case EntityState.Modified:
                    change.OriginalValue = SerializePropertyValue(property.OriginalValue);
                    change.NewValue = SerializePropertyValue(property.CurrentValue);
                    break;
            }

            changes.Add(change);
        }

        return changes;
    }

    /// <summary>
    /// Check if a property has the ExcludeFromAudit attribute
    /// </summary>
    /// <param name="entityType">The entity type</param>
    /// <param name="propertyName">The property name</param>
    /// <returns>True if the property should be excluded from audit</returns>
    public static bool HasExcludeFromAuditAttribute(Type entityType, string propertyName)
    {
        var property = entityType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        return property?.GetCustomAttribute<ExcludeFromAuditAttribute>() != null;
    }
}