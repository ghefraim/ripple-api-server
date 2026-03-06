using Application.Common.Interfaces.BlobStorage;
using Application.Common.Models.BlobStorage;

using Ardalis.GuardClauses;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace Application.Infrastructure.Services.BlobStorage;

public abstract class BaseStorageClient(string connectionString, string containerName) : IStorageClient
{
    private readonly BlobContainerClient _blobContainerClient =
        new BlobServiceClient(connectionString).GetBlobContainerClient(containerName);

    /// <summary>
    /// Function to upload blob file to container.
    /// </summary>
    /// <param name="inputBlob">Object containing name, data and metadata for blob to be uploaded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The name and URI of the uploaded blob.</returns>
    public async Task<FileStorage> UploadAsync(InputBlob inputBlob, CancellationToken cancellationToken)
    {
        BlobUploadOptions uploadOptions = new()
        {
            HttpHeaders = new BlobHttpHeaders()
            {
                ContentType = inputBlob.ContentType,
            },
            Tags = inputBlob.Tags,
            Metadata = inputBlob.Metadata,
        };

        BlobClient blob = _blobContainerClient.GetBlobClient(inputBlob.BlobName);

        switch (inputBlob.UploadType)
        {
            case UploadType.Binary:
                await blob.UploadAsync(inputBlob.BinaryFile, uploadOptions, cancellationToken);
                break;
            case UploadType.Stream:
                await blob.UploadAsync(inputBlob.StreamFile, uploadOptions, cancellationToken);
                break;
        }

        return new FileStorage()
        {
            BlobName = blob.Name,
            Uri = blob.Uri,
        };
    }

    /// <summary>
    /// Delete a blob file in the storage.
    /// </summary>
    /// <param name="blobName">Name of the blob to be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Void.</returns>
    public async Task DeleteFileAsync(string blobName, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(blobName);

        await _blobContainerClient.DeleteBlobIfExistsAsync(
            blobName,
            DeleteSnapshotsOption.None,
            default,
            cancellationToken);
    }

    /// <summary>
    /// Function to download large blobs.
    /// </summary>
    /// <param name="blobName">Name of the blob to be downloaded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Blob data as stream.</returns>
    public async Task<Stream> DownloadLargeBlobAsync(string blobName, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(blobName);

        BlobClient blob = _blobContainerClient.GetBlobClient(blobName);

        var response = await blob.DownloadStreamingAsync(default, cancellationToken);

        return response.Value.Content;
    }

    /// <summary>
    /// Function to download small blobs.
    /// </summary>
    /// <param name="blobName">Name of the blob to be downloaded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Blob data as byte array.</returns>
    public async Task<byte[]> DownloadSmallBlobAsync(string blobName, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(blobName);

        BlobClient blob = _blobContainerClient.GetBlobClient(blobName);

        var response = await blob.DownloadContentAsync(cancellationToken);

        return response.Value.Content.ToArray();
    }

    /// <summary>
    /// Function to determine if blob file exists.
    /// </summary>
    /// <param name="blobName">Name of the blob to be checked.</param>
    /// <returns>True if blob exists in container, false otherwise.</returns>
    public async Task<bool> FileExistsAsync(string blobName)
    {
        Guard.Against.NullOrWhiteSpace(blobName);

        return await _blobContainerClient.GetBlobClient(blobName).ExistsAsync();
    }

    /// <summary>
    /// Function to get blob data.
    /// </summary>
    /// <param name="blobName">Name of the blob queried.</param>
    /// <returns>Blob data, including metadata, tags and timestamps.</returns>
    public async Task<BlobProperties> GetBlobPropertiesAsync(string blobName)
    {
        Guard.Against.NullOrWhiteSpace(blobName);

        BlobClient blob = _blobContainerClient.GetBlobClient(blobName);

        var properties = await blob.GetPropertiesAsync();

        return properties.Value;
    }

    /// <summary>
    /// Function to retrieve the URI of a blob based on name.
    /// </summary>
    /// <param name="blobName">Name of the blob to find the URI of.</param>
    /// <returns>The URI of the blob.</returns>
    public Uri GetFileUri(string blobName)
    {
        Guard.Against.NullOrWhiteSpace(blobName);

        return _blobContainerClient.GetBlobClient(blobName).Uri;
    }

    /// <summary>
    /// Function to generate a SAS URI for a blob with read permissions.
    /// </summary>
    /// <param name="blobName">Name of the blob to generate SAS URI for.</param>
    /// <param name="expiration">Time span for how long the SAS URI should be valid.</param>
    /// <returns>The SAS URI of the blob.</returns>
    public Uri GetFileSasUri(string blobName, TimeSpan expiration)
    {
        Guard.Against.NullOrWhiteSpace(blobName);

        BlobClient blobClient = _blobContainerClient.GetBlobClient(blobName);

        // Check if the blob client can generate SAS URIs
        if (!blobClient.CanGenerateSasUri)
        {
            // If not, return the regular URI (this happens when using connection strings without account key)
            return blobClient.Uri;
        }

        // Create SAS permissions for read access
        BlobSasBuilder sasBuilder = new()
        {
            BlobContainerName = _blobContainerClient.Name,
            BlobName = blobName,
            Resource = "b", // "b" for blob
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiration),
        };

        // Set read permissions
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        // Generate the SAS URI
        return blobClient.GenerateSasUri(sasBuilder);
    }

    /// <summary>
    /// Function to set blob metadata.
    /// </summary>
    /// <param name="blobName">Name of the blob to be updated.</param>
    /// <param name="metadata">Dictionary containing new metadata.</param>
    /// <returns>Void.</returns>
    public async Task SetMetadataAsync(string blobName, IDictionary<string, string> metadata)
    {
        Guard.Against.NullOrWhiteSpace(blobName);

        BlobClient blob = _blobContainerClient.GetBlobClient(blobName);

        await blob.SetMetadataAsync(metadata);
    }

    /// <summary>
    /// Function to set blob tags.
    /// </summary>
    /// <param name="blobName">Name of the blob to be updated.</param>
    /// <param name="tags">Dictionary containing new tags.</param>
    /// <returns>Void.</returns>
    public async Task SetTagsAsync(string blobName, IDictionary<string, string> tags)
    {
        Guard.Against.NullOrWhiteSpace(blobName);

        BlobClient blob = _blobContainerClient.GetBlobClient(blobName);

        await blob.SetTagsAsync(tags);
    }
}

