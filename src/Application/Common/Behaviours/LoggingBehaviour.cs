using Application.Common.Interfaces;

using MediatR.Pipeline;

using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviours;

public class LoggingBehaviour<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly ILogger _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICorrelationIdService _correlationIdService;

    public LoggingBehaviour(
        ILogger<TRequest> logger,
        ICurrentUserService currentUserService,
        ICorrelationIdService correlationIdService)
    {
        _logger = logger;
        _currentUserService = currentUserService;
        _correlationIdService = correlationIdService;
    }

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _currentUserService.UserId?.ToString() ?? string.Empty;
        var correlationId = _correlationIdService.GetCorrelationId() ?? string.Empty;

        return Task.Run(
            () => _logger.LogInformation(
            "Request: {Name} UserId: {UserId} CorrelationId: {CorrelationId} {@Request}",
            requestName,
            userId,
            correlationId,
            request),
            cancellationToken);
    }
}