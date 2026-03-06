using Application.Common.Interfaces;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Application.Infrastructure.Services;

public class ApiKeyService : IApiKeyService
{
    private readonly ApplicationDbContext _context;
    private readonly IDateTime _dateTime;

    public ApiKeyService(ApplicationDbContext context, IDateTime dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<ApiKey?> ValidateApiKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _context.ApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k =>
                k.Key == key &&
                k.IsActive &&
                (!k.ExpiresAt.HasValue || k.ExpiresAt > _dateTime.Now),
                cancellationToken);
    }
}