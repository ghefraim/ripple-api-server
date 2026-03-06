using Application.Common.Models.BlobStorage;

using Azure.Storage.Blobs.Models;

namespace Application.Common.Interfaces.BlobStorage;

public interface IStorageClient
{
    Task<BlobProperties> GetBlobPropertiesAsync(string blobName);
    Task DeleteFileAsync(string blobName, CancellationToken cancellationToken);
    Task<byte[]> DownloadSmallBlobAsync(string blobName, CancellationToken cancellationToken);
    Task<Stream> DownloadLargeBlobAsync(string blobName, CancellationToken cancellationToken);
    Task<FileStorage> UploadAsync(InputBlob inputBlob, CancellationToken cancellationToken);
    Task<bool> FileExistsAsync(string blobName);
    Uri GetFileUri(string blobName);
    Uri GetFileSasUri(string blobName, TimeSpan expiration);
    Task SetMetadataAsync(string blobName, IDictionary<string, string> metadata);
    Task SetTagsAsync(string blobName, IDictionary<string, string> tags);
}

