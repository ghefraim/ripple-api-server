using System.Text.Json.Serialization;

namespace Application.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StripeEventStatus
{
    Received = 1,
    Processing = 2,
    Processed = 3,
    Failed = 4,
    Skipped = 5,
}
