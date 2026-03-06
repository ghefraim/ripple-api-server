using System.Text.Json.Serialization;

namespace Application.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrganizationRole
{
    Owner = 1,
    Member = 2,
}
