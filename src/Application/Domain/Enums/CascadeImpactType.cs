using System.Text.Json.Serialization;

namespace Application.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CascadeImpactType
{
    GateConflict = 1,
    TurnaroundBreach = 2,
    CrewGap = 3,
    DownstreamDelay = 4,
}
