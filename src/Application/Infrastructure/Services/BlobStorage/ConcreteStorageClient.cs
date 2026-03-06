using Application.Common.Interfaces.BlobStorage;
using Application.Common.Models.BlobStorage;

using Microsoft.Extensions.Options;

namespace Application.Infrastructure.Services.BlobStorage;

public class ConcreteStorageClient : BaseStorageClient, IConcreteStorageClient
{
    public ConcreteStorageClient(IOptions<AzureStorageOptions> options)
        : base(options.Value.ConnectionString, options.Value.ContainerName)
    {
    }
}
