using System.Text.Json.Serialization;

namespace Application.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DisruptionStatus
{
    Active = 1,
    Resolved = 2,
    Archived = 3,
}
