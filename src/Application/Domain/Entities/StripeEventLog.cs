using Application.Domain.Common;
using Application.Domain.Enums;

namespace Application.Domain.Entities;

public class StripeEventLog : BaseEntity
{
    public string StripeEventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public StripeEventStatus Status { get; set; }

    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }

    public DateTime ReceivedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
}
