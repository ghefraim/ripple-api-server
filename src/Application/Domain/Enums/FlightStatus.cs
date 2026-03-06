using System.Text.Json.Serialization;

namespace Application.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FlightStatus
{
    OnTime = 1,
    Delayed = 2,
    Cancelled = 3,
}
