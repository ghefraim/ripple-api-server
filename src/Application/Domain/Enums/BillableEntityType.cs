using System.Text.Json.Serialization;

namespace Application.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BillableEntityType
{
    Organization = 1,
    User = 2,
}
