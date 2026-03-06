using System.Text.Json.Serialization;

namespace Application.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FlightDirection
{
    Arrival = 1,
    Departure = 2,
}
