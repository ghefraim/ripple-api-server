using Application.Common.Interfaces;

using Microsoft.AspNetCore.Http;

namespace Application.Infrastructure.Services;

public class CorrelationIdService : ICorrelationIdService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string CorrelationIdKey = "CorrelationId";

    public CorrelationIdService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetCorrelationId()
    {
        return _httpContextAccessor.HttpContext?.Items[CorrelationIdKey]?.ToString();
    }
}
