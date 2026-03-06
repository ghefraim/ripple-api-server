using Application.Common.Interfaces;

using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviours;

public class UnhandledExceptionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<TRequest> _logger;
    private readonly ICorrelationIdService _correlationIdService;

    public UnhandledExceptionBehaviour(ILogger<TRequest> logger, ICorrelationIdService correlationIdService)
    {
        _logger = logger;
        _correlationIdService = correlationIdService;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            var requestName = typeof(TRequest).Name;
            var correlationId = _correlationIdService.GetCorrelationId() ?? string.Empty;

            _logger.LogError(ex, "Request: Unhandled Exception for Request {Name} CorrelationId: {CorrelationId} {@Request}",
                requestName, correlationId, request);

            throw;
        }
    }
}