using System.Text.Json.Serialization;

namespace Application.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationStatus
{
    Sent = 1,
    Acknowledged = 2,
    Handled = 3,
}
