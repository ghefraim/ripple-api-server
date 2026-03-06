using System.Text.Json.Serialization;

namespace Application.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GateSizeCategory
{
    Narrow = 1,
    Wide = 2,
}
