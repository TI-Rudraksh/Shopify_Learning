using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ShopifyIntegration.Models;

namespace ShopifyIntegration.Infrastructure.Shopify.Validators;

public interface IShopifyWebhookValidator
{
    ValidationResult Validate(byte[] rawBody, string hmacHeader);
}

public sealed record ValidationResult(bool IsValid)
{
    public static readonly ValidationResult Valid   = new(true);
    public static readonly ValidationResult Invalid = new(false);
}

public sealed class ShopifyWebhookValidator : IShopifyWebhookValidator
{
    private readonly ShopifySettings _settings;
    private readonly ILogger<ShopifyWebhookValidator> _logger;

    public ShopifyWebhookValidator(IOptions<ShopifySettings> options, ILogger<ShopifyWebhookValidator> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public ValidationResult Validate(byte[] rawBody, string hmacHeader)
    {
        if (string.IsNullOrEmpty(_settings.WebhookSecret))
        {
            _logger.LogWarning("WebhookSecret is not configured");
            return ValidationResult.Invalid;
        }
        
        if (rawBody.Length == 0)
        {
            _logger.LogWarning("Webhook body is empty");
            return ValidationResult.Invalid;
        }

        var keyBytes = Encoding.UTF8.GetBytes(_settings.WebhookSecret);
        using var hmac = new HMACSHA256(keyBytes);
        var computedHash = hmac.ComputeHash(rawBody);
        var computedBase64 = Convert.ToBase64String(computedHash);

        byte[] actualBytes;
        try
        {
            actualBytes = Convert.FromBase64String(hmacHeader);
        }
        catch (FormatException)
        {
            _logger.LogWarning("HMAC header is not valid Base64: {Header}", hmacHeader);
            return ValidationResult.Invalid;
        }

        var isValid = CryptographicOperations.FixedTimeEquals(computedHash, actualBytes);
        
        if (!isValid)
        {
            _logger.LogWarning(
                "HMAC validation failed. Computed: {Computed}, Received: {Received}, Secret length: {SecretLength}",
                computedBase64, hmacHeader, _settings.WebhookSecret.Length);
        }

        return isValid ? ValidationResult.Valid : ValidationResult.Invalid;
    }
}
