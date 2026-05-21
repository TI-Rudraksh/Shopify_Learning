using Hangfire;
using MediatR;
using ShopifyIntegration.Features.Orders.Commands;
using ShopifyIntegration.Infrastructure.Shopify;

namespace ShopifyIntegration.Jobs;

/// <summary>
/// Fire-and-forget / retriable background job that fulfills all open fulfillment
/// orders for a given Shopify order.
///
/// Decouples the HTTP response from the Shopify API call so the controller can
/// return 202 Accepted immediately. Hangfire retries automatically on transient
/// failures with exponential back-off.
///
/// Retry schedule (seconds): 30 → 120 → 300  (3 attempts total)
/// On Shopify throttle errors the job is rescheduled with an extra 10-second delay
/// on top of the normal back-off so the leaky-bucket has time to refill.
/// </summary>
public sealed class FulfillOrderJob
{
    private readonly IMediator _mediator;
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<FulfillOrderJob> _logger;

    public FulfillOrderJob(
        IMediator mediator,
        IBackgroundJobClient jobs,
        ILogger<FulfillOrderJob> logger)
    {
        _mediator = mediator;
        _jobs     = jobs;
        _logger   = logger;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [30, 120, 300],
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task ExecuteAsync(
        string  orderId,
        string? trackingNumber,
        string? trackingCompany,
        bool    notifyCustomer,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "FulfillOrderJob: starting fulfillment for order {OrderId}.", orderId);

        try
        {
            var result = await _mediator.Send(
                new FulfillOrderCommand(orderId, trackingNumber, trackingCompany, notifyCustomer),
                ct);

            _logger.LogInformation(
                "FulfillOrderJob: fulfilled order {OrderId} → fulfillment {FulfillmentGid} ({Status}).",
                orderId, result.FulfillmentGid, result.Status);
        }
        catch (ShopifyFulfillmentException ex)
            when (ex.ShopifyErrors.Any(e => e.Contains("Throttled", StringComparison.OrdinalIgnoreCase)))
        {
            // Rate-limited: schedule a fresh attempt in 10 s instead of failing immediately.
            _logger.LogWarning(
                "FulfillOrderJob: Shopify rate-limited for order {OrderId}. Rescheduling in 10 s.",
                orderId);

            _jobs.Schedule<FulfillOrderJob>(
                job => job.ExecuteAsync(orderId, trackingNumber, trackingCompany, notifyCustomer,
                    CancellationToken.None),
                TimeSpan.FromSeconds(10));
        }
    }
}
