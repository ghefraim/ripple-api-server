using Application.Domain.Entities;

namespace Application.Common.Interfaces;

public interface IApiKeyService
{
    Task<ApiKey?> ValidateApiKeyAsync(string key, CancellationToken cancellationToken = default);
}