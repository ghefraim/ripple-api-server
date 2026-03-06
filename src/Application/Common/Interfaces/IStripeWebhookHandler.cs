namespace Application.Common.Interfaces;

public interface IStripeWebhookHandler
{
    Task HandleAsync(string payload, string signature, CancellationToken cancellationToken = default);
}
