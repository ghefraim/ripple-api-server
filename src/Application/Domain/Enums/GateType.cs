using System.Text.Json.Serialization;

namespace Application.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GateType
{
    Domestic = 1,
    International = 2,
    Both = 3,
}
