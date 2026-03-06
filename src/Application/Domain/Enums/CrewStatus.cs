using System.Text.Json.Serialization;

namespace Application.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CrewStatus
{
    Available = 1,
    Assigned = 2,
    OnBreak = 3,
}
