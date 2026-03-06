namespace Application.Common.Models.User;

public class UserSessionResponse
{
    public string DeviceInfo { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? LastUsedDate { get; set; }
    public DateTime ExpiryDate { get; set; }
}