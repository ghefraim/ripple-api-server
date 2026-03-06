using System.Text.Json.Serialization;

namespace Application.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BillingInterval
{
    Monthly = 1,
    Annual = 2,
}
