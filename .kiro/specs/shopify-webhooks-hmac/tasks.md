# Implementation Plan: Shopify Webhooks HMAC

## Overview

Implement inbound Shopify webhook support for the existing .NET 10 ASP.NET Core project. The implementation adds HMAC-SHA256 signature validation, strongly-typed payload models, a webhook service with topic dispatch, and a dedicated controller endpoint — all wired into the existing DI container and configuration system.

## Tasks

- [x] 1. Extend ShopifySettings and add webhook models
  - [x] 1.1 Add `WebhookSecret` property to `ShopifyIntegration/Models/ShopifySettings.cs`
    - Add `public string WebhookSecret { get; set; } = string.Empty;` to the existing `ShopifySettings` class
    - _Requirements: 6.1, 6.3_

  - [x] 1.2 Create `ShopifyIntegration/Webhooks/Models/Products/ProductCreatedWebhook.cs`
    - Define `sealed class ProductCreatedWebhook` with `[JsonProperty]` attributes for `id`, `title`, `vendor`, `status`, `updated_at`
    - Properties: `Id` (long), `Title` (string), `Vendor` (string), `Status` (string), `UpdatedAt` (DateTimeOffset)
    - Use `Newtonsoft.Json` `[JsonProperty]` attributes to map snake_case Shopify field names
    - _Requirements: 4.1, 4.5_

  - [x] 1.3 Create `ShopifyIntegration/Webhooks/Models/Products/ProductUpdatedWebhook.cs`
    - Define `sealed class ProductUpdatedWebhook` with the same field set as `ProductCreatedWebhook`
    - _Requirements: 4.2, 4.6_

  - [x] 1.4 Create `ShopifyIntegration/Webhooks/Models/Products/ProductDeletedWebhook.cs`
    - Define `sealed class ProductDeletedWebhook` with only `Id` (long) mapped from `[JsonProperty("id")]`
    - _Requirements: 4.3, 4.7_

  - [ ]* 1.5 Write property test for webhook model deserialization round-trip
    - Create `ShopifyIntegration.Tests/ShopifyWebhookServiceTests.cs`
    - **Property 3: Webhook Model Deserialization Round-Trip**
    - Use `[Property]` attribute with FsCheck; generate arbitrary instances of `ProductCreatedWebhook`, `ProductUpdatedWebhook`, and `ProductDeletedWebhook`
    - Serialize with `JsonConvert.SerializeObject`, deserialize back, assert all fields equal the originals
    - **Validates: Requirements 4.1, 4.2, 4.3, 4.5, 4.6, 4.7**

- [x] 2. Implement ShopifyWebhookValidator
  - [x] 2.1 Create `ShopifyIntegration/Webhooks/Validators/ShopifyWebhookValidator.cs`
    - Define `ValidationResult` sealed record with `IsValid` bool and static `Valid`/`Invalid` singletons
    - Define `IShopifyWebhookValidator` interface with `Validate(byte[] rawBody, string hmacHeader)` method
    - Implement `ShopifyWebhookValidator` using `IOptions<ShopifySettings>` constructor injection
    - Guard: return `Invalid` when `WebhookSecret` is null/empty or `rawBody` is empty
    - Compute HMAC-SHA256 using `System.Security.Cryptography.HMACSHA256`
    - Base64-encode the digest; decode both expected and incoming header bytes; compare with `CryptographicOperations.FixedTimeEquals`
    - Catch `FormatException` on Base64 decode and return `Invalid`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 6.2_

  - [ ]* 2.2 Write property test for HMAC signature round-trip
    - Create `ShopifyIntegration.Tests/ShopifyWebhookValidatorTests.cs`
    - **Property 1: HMAC Signature Round-Trip**
    - Use `[Property]` with `NonEmptyArray<byte>` for body and `NonEmptyString` for secret
    - Instantiate `ShopifyWebhookValidator` with mocked `IOptions<ShopifySettings>`; compute the expected signature, then call `Validate` with it
    - Assert `ValidationResult.IsValid == true`
    - **Validates: Requirements 2.1, 2.2, 2.4, 2.8**

  - [ ]* 2.3 Write property test for tampered signature rejection
    - In `ShopifyIntegration.Tests/ShopifyWebhookValidatorTests.cs`
    - **Property 2: Tampered Signature Is Always Rejected**
    - Use `[Property]` with `NonEmptyArray<byte>` for body, `NonEmptyString` for secret, `NonEmptyString` for tampered signature (filter out the correct signature)
    - Assert `ValidationResult.IsValid == false`
    - **Validates: Requirements 2.5**

  - [ ]* 2.4 Write unit tests for ShopifyWebhookValidator edge cases
    - In `ShopifyIntegration.Tests/ShopifyWebhookValidatorTests.cs`
    - Test: empty `WebhookSecret` → `Invalid`
    - Test: empty body bytes → `Invalid`
    - Test: malformed Base64 in HMAC header → `Invalid`
    - _Requirements: 2.6, 2.7_

- [x] 3. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement ShopifyWebhookService
  - [x] 4.1 Create `ShopifyIntegration/Webhooks/Services/ShopifyWebhookService.cs`
    - Define `WebhookResult` sealed record with `IsSuccess` bool, optional `ErrorMessage`, and static `Success`/`Failure(string)` factory
    - Define `IShopifyWebhookService` interface with `Task<WebhookResult> ProcessAsync(byte[] rawBody, string topic)`
    - Implement `ShopifyWebhookService` with `ILogger<ShopifyWebhookService>` constructor injection
    - In `ProcessAsync`: decode `rawBody` to UTF-8 string, switch on `topic`
    - `products/create`: deserialize to `ProductCreatedWebhook` via `JsonConvert.DeserializeObject<T>`, log product ID and title at `Information` level
    - `products/update`: deserialize to `ProductUpdatedWebhook`, log product ID and title at `Information` level
    - `products/delete`: deserialize to `ProductDeletedWebhook`, log product ID at `Information` level
    - Unknown topic: log at `Warning` level, return `WebhookResult.Success`
    - Catch all exceptions: log at `Error` level with topic and exception, return `WebhookResult.Failure(ex.Message)`
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 5.4_

  - [ ]* 4.2 Write unit tests for ShopifyWebhookService
    - In `ShopifyIntegration.Tests/ShopifyWebhookServiceTests.cs`
    - Test: valid `products/create` payload → `WebhookResult.Success`, product ID and title logged
    - Test: valid `products/update` payload → `WebhookResult.Success`, product ID and title logged
    - Test: valid `products/delete` payload → `WebhookResult.Success`, product ID logged
    - Test: unknown topic → `WebhookResult.Success`, warning logged
    - Test: malformed JSON body → `WebhookResult.Failure`, error logged
    - _Requirements: 4.4, 5.1, 5.2, 5.3, 5.4_

- [x] 5. Implement WebhooksController
  - [x] 5.1 Create `ShopifyIntegration/Controllers/WebhooksController.cs`
    - Define `sealed class WebhooksController : ControllerBase` with `[ApiController]` and `[Route("api/webhooks")]`
    - Constructor-inject `IShopifyWebhookValidator` and `IShopifyWebhookService`
    - Add `[HttpPost("shopify")]` action `ReceiveShopifyWebhook()`
    - Extract `X-Shopify-Hmac-SHA256` header; return `BadRequest()` if absent or empty
    - Extract `X-Shopify-Topic` header; return `BadRequest()` if absent or empty
    - Read raw body via `MemoryStream` + `Request.Body.CopyToAsync`
    - Call `_validator.Validate(rawBody, hmacHeader)`; return `Unauthorized()` (empty body) if invalid
    - Call `await _service.ProcessAsync(rawBody, topic)`; return `Ok()` on success, `StatusCode(500)` on failure
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 3.1, 3.2, 3.3, 5.5, 5.6_

  - [ ]* 5.2 Write unit tests for WebhooksController
    - Create `ShopifyIntegration.Tests/WebhooksControllerTests.cs`
    - Use mock `IShopifyWebhookValidator` and `IShopifyWebhookService`
    - Test: missing `X-Shopify-Hmac-SHA256` → 400 Bad Request
    - Test: missing `X-Shopify-Topic` → 400 Bad Request
    - Test: validator returns `Invalid` → 401 Unauthorized, service not called
    - Test: validator returns `Valid`, service returns `Success` → 200 OK
    - Test: validator returns `Valid`, service returns `Failure` → 500 Internal Server Error
    - _Requirements: 1.5, 1.6, 3.1, 3.2, 3.3, 5.5, 5.6_

- [x] 6. Register services in Program.cs
  - [x] 6.1 Update `ShopifyIntegration/Program.cs` to register webhook services
    - Add `builder.Services.AddScoped<IShopifyWebhookValidator, ShopifyWebhookValidator>()`
    - Add `builder.Services.AddScoped<IShopifyWebhookService, ShopifyWebhookService>()`
    - Ensure `ShopifySettings` configuration binding already present (it is — no change needed)
    - _Requirements: 6.4_

  - [ ]* 6.2 Write DI registration smoke test
    - In `ShopifyIntegration.Tests/WebhooksControllerTests.cs` or a dedicated `DiRegistrationTests.cs`
    - Build a `WebApplication` using `Program.cs` registrations and verify `IShopifyWebhookValidator`, `IShopifyWebhookService`, and `WebhooksController` resolve without exceptions
    - _Requirements: 6.4_

- [x] 7. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for a faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests use FsCheck 2.16.6 with `[Property]` attributes (already in `ShopifyIntegration.Tests.csproj`)
- Unit tests use xUnit (already configured)
- All JSON serialization uses `Newtonsoft.Json` (`JsonConvert`) — already a project dependency
- No new NuGet packages are required
