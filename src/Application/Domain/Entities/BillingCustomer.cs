using Application.Domain.Common;
using Application.Domain.Enums;

namespace Application.Domain.Entities;

public class BillingCustomer : AuditableEntity
{
    public BillableEntityType EntityType { get; set; }

    public Guid EntityId { get; set; }

    public string StripeCustomerId { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Name { get; set; }
}
