using System.ComponentModel.DataAnnotations;

using Application.Domain.Common;

namespace Application.Domain.Entities;

public class ApiKey : AuditableEntity, IOrganizationScoped
{
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;

    public ApplicationUser User { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
}