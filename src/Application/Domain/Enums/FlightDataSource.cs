using System.Text.Json.Serialization;

namespace Application.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FlightDataSource
{
    Manual = 1,
    AviationApi = 2,
    Aodb = 3,
}
