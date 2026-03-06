using System.Text.Json.Serialization;

namespace Application.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EntitlementSource
{
    Plan = 1,
    Addon = 2,
    Manual = 3,
    Promo = 4,
    Legacy = 5,
}
