namespace Application.Common.Models;

public class StripeOptions
{
    public const string Stripe = "Stripe";

    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
}

public class BillingOptions
{
    public const string Billing = "Billing";

    public string DefaultBillableEntityType { get; set; } = "Organization";
    public int EntitlementCacheDurationMinutes { get; set; } = 5;
}
