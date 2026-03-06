using Application.Common.Models;
using Application.Common.Models.Audit;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Audit.GetAuditLogs;

public record GetAuditLogsQuery(
    string? EntityName = null,
    string? EntityId = null,
    string? Action = null,
    string? UserId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 10
) : IRequest<PagedList<AuditLogDto>>;

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public List<AuditDetailInfo> Details { get; set; } = [];
}

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, PagedList<AuditLogDto>>
{
    private readonly ApplicationDbContext _context;

    public GetAuditLogsQueryHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedList<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.AuditLogs.AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(request.EntityName))
        {
            query = query.Where(a => a.EntityName == request.EntityName);
        }

        if (!string.IsNullOrWhiteSpace(request.EntityId))
        {
            query = query.Where(a => a.EntityId == request.EntityId);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = query.Where(a => a.Action == request.Action);
        }

        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            query = query.Where(a => a.UserId == request.UserId);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= request.ToDate.Value);
        }

        // Order by timestamp descending (most recent first)
        query = query.OrderByDescending(a => a.Timestamp);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtoItems = items.Select(MapToDto).ToList();

        return new PagedList<AuditLogDto>(dtoItems, totalCount, request.Page, request.PageSize);
    }

    private static AuditLogDto MapToDto(AuditEntry auditEntry)
    {
        return new AuditLogDto
        {
            Id = auditEntry.Id,
            EntityName = auditEntry.EntityName,
            EntityId = auditEntry.EntityId,
            Action = auditEntry.Action,
            Timestamp = auditEntry.Timestamp,
            UserId = auditEntry.UserId,
            UserEmail = auditEntry.UserEmail,
            Details = auditEntry.GetDetails()
        };
    }
}
