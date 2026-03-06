using System.Text.Json.Serialization;

namespace Application.Common.Models.BlobStorage;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UploadType
{
    Binary,
    Stream,
}

