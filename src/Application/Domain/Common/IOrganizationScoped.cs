namespace Application.Domain.Common;

/// <summary>
/// Interface for entities that belong to a specific organization.
/// Entities implementing this interface will have automatic query filtering
/// to ensure data isolation between organizations.
/// </summary>
public interface IOrganizationScoped
{
    Guid OrganizationId { get; set; }
}
