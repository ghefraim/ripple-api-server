using System.Security.Cryptography;

using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;

namespace Application.Features.ApiKeys.CreateApiKey;

[Authorize]
public record CreateApiKeyCommand(string Name, DateTime? ExpiresAt) : IRequest<ApiKeyResponse>;

public record ApiKeyResponse(string Key, string Name, DateTime Created, DateTime? ExpiresAt);

public class CreateApiKeyCommandHandler(ApplicationDbContext context, ICurrentUserService currentUserService) : IRequestHandler<CreateApiKeyCommand, ApiKeyResponse>
{
    private readonly ApplicationDbContext _context = context;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<ApiKeyResponse> Handle(CreateApiKeyCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var apiKey = new ApiKey
        {
            UserId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException()),
            OrganizationId = organizationId,
            Name = request.Name,
            Key = GenerateSecureApiKey(),
            ExpiresAt = request.ExpiresAt,
            IsActive = true,
        };

        try
        {
            await _context.ApiKeys.AddAsync(apiKey, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to create API key.", ex);
        }

        return new ApiKeyResponse(apiKey.Key, apiKey.Name, apiKey.CreatedOn, apiKey.ExpiresAt);
    }

    private static string GenerateSecureApiKey()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        var key = Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        return $"sk_{key}";
    }
}
