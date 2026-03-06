using System.Text.Json.Serialization;

namespace Application.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FlightType
{
    Domestic = 1,
    International = 2,
}
