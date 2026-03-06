using System.Text.Json.Serialization;

namespace Application.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DisruptionType
{
    Delay = 1,
    Cancellation = 2,
    GateChange = 3,
}
