using Application.Common.Security;
using Application.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Application.Features.ApiKeys.GetApiKeys;

[Authorize]
public record GetApiKeysQuery : IRequest<List<ApiKeyResponse>>;

public record ApiKeyResponse(string Key, string Name, DateTime Created, DateTime? ExpiresAt);

public class GetApiKeysQueryHandler(ApplicationDbContext context) : IRequestHandler<GetApiKeysQuery, List<ApiKeyResponse>>
{
    private readonly ApplicationDbContext _context = context;
    public async Task<List<ApiKeyResponse>> Handle(GetApiKeysQuery request, CancellationToken cancellationToken)
    {
        return await _context.ApiKeys
            .Select(x => new ApiKeyResponse(x.Key, x.Name, x.CreatedOn, x.ExpiresAt))
            .ToListAsync(cancellationToken);
    }
}
