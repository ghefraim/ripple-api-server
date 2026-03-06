using System.ComponentModel.DataAnnotations;

using Application.Domain.Common;

namespace Application.Domain.Entities;

public class UserSession : ISoftDeletable
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    [Required]
    [MaxLength(200)]
    public string RefreshToken { get; set; } = null!;
    public DateTime RefreshTokenExpiryDate { get; set; }

    [MaxLength(500)]
    public string DeviceInfo { get; set; } = string.Empty;
    [MaxLength(45)]
    public string? IpAddress { get; set; }
    [MaxLength(1000)]
    public string? UserAgent { get; set; }

    public DateTime LastUsedDate { get; set; }
    public bool IsActive { get; set; } = true;

    // Future mobile app support
    public string? PushNotificationToken { get; set; }

    // Navigation property
    public ApplicationUser User { get; set; } = null!;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedOn { get; set; }
}