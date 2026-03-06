namespace Application.Common.Models.BlobStorage;

public class AzureStorageOptions
{
    public const string AzureStorage = "AzureStorage";
    public required string ConnectionString { get; set; }
    public required string ContainerName { get; set; }
}
