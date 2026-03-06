using System.Text.Json.Serialization;

namespace Application.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Severity
{
    Info = 1,
    Warning = 2,
    Critical = 3,
}
