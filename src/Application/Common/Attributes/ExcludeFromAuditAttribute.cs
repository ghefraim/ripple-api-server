namespace Application.Common.Attributes;

/// <summary>
/// Attribute to exclude properties from audit tracking
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ExcludeFromAuditAttribute : Attribute
{
}