using System.Diagnostics;
using MediatR;

namespace ShopifyIntegration.Pipeline;

public sealed class LoggingBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;

    public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var correlationId = Guid.NewGuid();
        _logger.LogInformation(
            "[{CorrelationId}] Handling {RequestName}", correlationId, requestName);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();
            _logger.LogInformation(
                "[{CorrelationId}] Handled {RequestName} in {ElapsedMs}ms",
                correlationId, requestName, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "[{CorrelationId}] {RequestName} failed after {ElapsedMs}ms",
                correlationId, requestName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
