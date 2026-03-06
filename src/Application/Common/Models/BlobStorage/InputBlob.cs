namespace Application.Common.Models.BlobStorage;

public class InputBlob
{
    public Stream? StreamFile { get; init; }
    public BinaryData? BinaryFile { get; init; }
    public UploadType UploadType { get; init; }
    public required string BlobName { get; init; }
    public required string ContentType { get; init; }
    public IDictionary<string, string>? Tags { get; init; }
    public IDictionary<string, string>? Metadata { get; init; }
}

