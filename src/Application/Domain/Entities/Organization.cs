using System.ComponentModel.DataAnnotations;

using Application.Domain.Common;

namespace Application.Domain.Entities;

public class Organization : ISoftDeletable
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedOn { get; set; }

    public ICollection<UserOrganization> UserOrganizations { get; set; } = new List<UserOrganization>();
}
