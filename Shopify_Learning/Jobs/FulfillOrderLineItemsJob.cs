using Hangfire;
using MediatR;
using ShopifyIntegration.Features.Orders.Commands;
using ShopifyIntegration.Infrastructure.Shopify;

namespace ShopifyIntegration.Jobs;

/// <summary>
/// Fire-and-forget / retriable background job that partially fulfills specific
/// line items of a Shopify order.
///
/// Same retry and rate-limit strategy as <see cref="FulfillOrderJob"/>.
/// </summary>
public sealed class FulfillOrderLineItemsJob
{
    private readonly IMediator _mediator;
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<FulfillOrderLineItemsJob> _logger;

    public FulfillOrderLineItemsJob(
        IMediator mediator,
        IBackgroundJobClient jobs,
        ILogger<FulfillOrderLineItemsJob> logger)
    {
        _mediator = mediator;
        _jobs     = jobs;
        _logger   = logger;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [30, 120, 300],
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task ExecuteAsync(
        string       orderId,
        List<string> lineItemIds,
        string?      trackingNumber,
        string?      trackingCompany,
        bool         notifyCustomer,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "FulfillOrderLineItemsJob: starting partial fulfillment for order {OrderId} " +
            "({Count} line item(s)).", orderId, lineItemIds.Count);

        try
        {
            var result = await _mediator.Send(
                new FulfillOrderLineItemsCommand(
                    orderId, lineItemIds, trackingNumber, trackingCompany, notifyCustomer),
                ct);

            _logger.LogInformation(
                "FulfillOrderLineItemsJob: fulfilled order {OrderId} → fulfillment {FulfillmentGid} ({Status}).",
                orderId, result.FulfillmentGid, result.Status);
        }
        catch (ShopifyFulfillmentException ex)
            when (ex.ShopifyErrors.Any(e => e.Contains("Throttled", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning(
                "FulfillOrderLineItemsJob: Shopify rate-limited for order {OrderId}. Rescheduling in 10 s.",
                orderId);

            _jobs.Schedule<FulfillOrderLineItemsJob>(
                job => job.ExecuteAsync(orderId, lineItemIds, trackingNumber, trackingCompany,
                    notifyCustomer, CancellationToken.None),
                TimeSpan.FromSeconds(10));
        }
    }
}
