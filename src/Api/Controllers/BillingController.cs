using Application.Common.Interfaces;
using Application.Features.Billing.CancelSubscription;
using Application.Features.Billing.CreateCheckoutSession;
using Application.Features.Billing.CreateCustomerPortalSession;
using Application.Features.Billing.GetCurrentSubscription;
using Application.Features.Billing.GetEntitlements;
using Application.Features.Billing.GetPlans;
using Application.Features.Billing.GrantEntitlement;
using Application.Features.Billing.RevokeEntitlement;
using Application.Features.Billing.SyncSubscription;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

public class BillingController : ApiControllerBase
{
    private readonly IStripeWebhookHandler _webhookHandler;

    public BillingController(IStripeWebhookHandler webhookHandler)
    {
        _webhookHandler = webhookHandler;
    }

    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlans()
    {
        return Ok(await Mediator.Send(new GetPlansQuery()));
    }

    [HttpGet("subscription")]
    [Authorize]
    public async Task<IActionResult> GetCurrentSubscription()
    {
        return Ok(await Mediator.Send(new GetCurrentSubscriptionQuery()));
    }

    [HttpPost("subscription/sync")]
    [Authorize]
    public async Task<IActionResult> SyncSubscription()
    {
        return Ok(await Mediator.Send(new SyncSubscriptionCommand()));
    }

    [HttpGet("entitlements")]
    [Authorize]
    public async Task<IActionResult> GetEntitlements()
    {
        return Ok(await Mediator.Send(new GetEntitlementsQuery()));
    }

    [HttpPost("checkout")]
    [Authorize]
    public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutSessionCommand command)
    {
        return Ok(await Mediator.Send(command));
    }

    [HttpPost("portal")]
    [Authorize]
    public async Task<IActionResult> CreateCustomerPortalSession([FromBody] CreateCustomerPortalSessionCommand command)
    {
        return Ok(await Mediator.Send(command));
    }

    [HttpPost("cancel")]
    [Authorize]
    public async Task<IActionResult> CancelSubscription([FromBody] CancelSubscriptionCommand command)
    {
        return Ok(await Mediator.Send(command));
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleWebhook()
    {
        var payload = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();

        if (string.IsNullOrEmpty(signature))
        {
            return BadRequest("Missing Stripe-Signature header");
        }

        try
        {
            await _webhookHandler.HandleAsync(payload, signature);
            return Ok();
        }
        catch (Stripe.StripeException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("entitlements/grant")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GrantEntitlement([FromBody] GrantEntitlementCommand command)
    {
        return Ok(await Mediator.Send(command));
    }

    [HttpDelete("entitlements/revoke")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RevokeEntitlement([FromBody] RevokeEntitlementCommand command)
    {
        return Ok(await Mediator.Send(command));
    }
}
