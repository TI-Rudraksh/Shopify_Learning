# Requirements Document

## Introduction

This feature adds Shopify webhook support to the existing .NET ASP.NET Core integration project. The system will receive inbound HTTP POST requests from Shopify for product lifecycle events (created, updated, deleted), verify each request's authenticity using HMAC-SHA256 signature validation, and dispatch the validated payload to the appropriate processing logic. This ensures the application reacts to Shopify catalog changes in real time while rejecting forged or tampered requests.

## Glossary

- **WebhooksController**: The ASP.NET Core controller that exposes the webhook endpoint and routes incoming Shopify webhook requests.
- **ShopifyWebhookValidator**: The component responsible for computing and comparing the HMAC-SHA256 signature of an incoming webhook request against the value provided in the `X-Shopify-Hmac-SHA256` header.
- **ShopifyWebhookService**: The service that receives a validated webhook payload and dispatches it to the correct handler based on the event topic.
- **WebhookPayload**: The deserialized JSON body of an incoming Shopify webhook request.
- **ProductCreatedWebhook**: The model representing the payload of a `products/create` Shopify webhook event.
- **ProductUpdatedWebhook**: The model representing the payload of a `products/update` Shopify webhook event.
- **ProductDeletedWebhook**: The model representing the payload of a `products/delete` Shopify webhook event.
- **ShopifySettings**: The existing configuration model that holds Shopify credentials and store settings; extended to include the webhook secret.
- **WebhookSecret**: The shared secret configured in Shopify and stored in `ShopifySettings`, used as the HMAC-SHA256 key for signature verification.
- **X-Shopify-Hmac-SHA256**: The HTTP request header that Shopify populates with the Base64-encoded HMAC-SHA256 signature of the raw request body.
- **X-Shopify-Topic**: The HTTP request header that identifies the event type (e.g., `products/create`).

---

## Requirements

### Requirement 1: Webhook Endpoint

**User Story:** As a developer, I want a dedicated HTTP endpoint that receives Shopify webhook POST requests, so that the application can react to product lifecycle events from Shopify.

#### Acceptance Criteria

1. THE WebhooksController SHALL expose a POST endpoint at the route `/api/webhooks/shopify`.
2. WHEN a POST request is received at `/api/webhooks/shopify`, THE WebhooksController SHALL read the raw request body before any model binding occurs.
3. WHEN a POST request is received at `/api/webhooks/shopify`, THE WebhooksController SHALL extract the `X-Shopify-Hmac-SHA256` header value from the request.
4. WHEN a POST request is received at `/api/webhooks/shopify`, THE WebhooksController SHALL extract the `X-Shopify-Topic` header value from the request.
5. WHEN the `X-Shopify-Hmac-SHA256` header is absent from the request, THE WebhooksController SHALL return HTTP 400 Bad Request.
6. WHEN the `X-Shopify-Topic` header is absent from the request, THE WebhooksController SHALL return HTTP 400 Bad Request.

---

### Requirement 2: HMAC-SHA256 Signature Validation

**User Story:** As a developer, I want every incoming webhook request to be verified using HMAC-SHA256, so that the application only processes authentic requests originating from Shopify.

#### Acceptance Criteria

1. THE ShopifyWebhookValidator SHALL compute an HMAC-SHA256 digest of the raw request body bytes using the WebhookSecret as the key.
2. THE ShopifyWebhookValidator SHALL Base64-encode the computed HMAC-SHA256 digest to produce the expected signature.
3. WHEN the expected signature is compared to the value in the `X-Shopify-Hmac-SHA256` header, THE ShopifyWebhookValidator SHALL use a constant-time comparison to prevent timing attacks.
4. WHEN the computed signature matches the header value, THE ShopifyWebhookValidator SHALL return a valid result indicating the request is authentic.
5. WHEN the computed signature does not match the header value, THE ShopifyWebhookValidator SHALL return an invalid result indicating the request is not authentic.
6. WHEN the WebhookSecret is null or empty, THE ShopifyWebhookValidator SHALL return an invalid result.
7. WHEN the raw request body is empty, THE ShopifyWebhookValidator SHALL return an invalid result.
8. FOR ALL valid raw body byte arrays and WebhookSecret values, computing the signature and then verifying it against the computed value SHALL produce a valid result (round-trip property).

---

### Requirement 3: Request Authentication Enforcement

**User Story:** As a developer, I want the webhook endpoint to reject unauthenticated requests, so that only verified Shopify events are processed.

#### Acceptance Criteria

1. WHEN the ShopifyWebhookValidator returns an invalid result for a request, THE WebhooksController SHALL return HTTP 401 Unauthorized without invoking the ShopifyWebhookService.
2. WHEN the ShopifyWebhookValidator returns a valid result for a request, THE WebhooksController SHALL pass the raw body and topic to the ShopifyWebhookService for processing.
3. WHEN the WebhooksController returns HTTP 401 Unauthorized, THE WebhooksController SHALL not include details about the validation failure in the response body.

---

### Requirement 4: Webhook Payload Deserialization

**User Story:** As a developer, I want incoming webhook JSON payloads to be deserialized into strongly-typed models, so that downstream processing logic can work with structured data.

#### Acceptance Criteria

1. WHEN the topic is `products/create`, THE ShopifyWebhookService SHALL deserialize the raw body into a ProductCreatedWebhook model.
2. WHEN the topic is `products/update`, THE ShopifyWebhookService SHALL deserialize the raw body into a ProductUpdatedWebhook model.
3. WHEN the topic is `products/delete`, THE ShopifyWebhookService SHALL deserialize the raw body into a ProductDeletedWebhook model.
4. WHEN the JSON body cannot be deserialized into the expected model, THE ShopifyWebhookService SHALL log the deserialization error and return a failure result.
5. THE ProductCreatedWebhook model SHALL include at minimum the fields: `Id` (long), `Title` (string), `Vendor` (string), `Status` (string), and `UpdatedAt` (DateTimeOffset).
6. THE ProductUpdatedWebhook model SHALL include at minimum the fields: `Id` (long), `Title` (string), `Vendor` (string), `Status` (string), and `UpdatedAt` (DateTimeOffset).
7. THE ProductDeletedWebhook model SHALL include at minimum the fields: `Id` (long).

---

### Requirement 5: Webhook Event Processing

**User Story:** As a developer, I want each validated webhook event to be handled according to its topic, so that the application can take appropriate action for each product lifecycle change.

#### Acceptance Criteria

1. WHEN a `products/create` event is processed, THE ShopifyWebhookService SHALL log the product ID and title of the created product.
2. WHEN a `products/update` event is processed, THE ShopifyWebhookService SHALL log the product ID and title of the updated product.
3. WHEN a `products/delete` event is processed, THE ShopifyWebhookService SHALL log the product ID of the deleted product.
4. WHEN the topic does not match any known event type, THE ShopifyWebhookService SHALL log the unrecognized topic and return a success result without throwing an exception.
5. WHEN a webhook event is successfully processed, THE WebhooksController SHALL return HTTP 200 OK.
6. WHEN the ShopifyWebhookService returns a failure result, THE WebhooksController SHALL return HTTP 500 Internal Server Error.

---

### Requirement 6: Configuration

**User Story:** As a developer, I want the webhook secret to be loaded from application configuration, so that it can be managed securely without hardcoding.

#### Acceptance Criteria

1. THE ShopifySettings model SHALL include a `WebhookSecret` property of type string.
2. THE ShopifyWebhookValidator SHALL read the WebhookSecret exclusively from the injected `IOptions<ShopifySettings>` instance.
3. WHEN the `Shopify:WebhookSecret` configuration key is absent, THE ShopifyWebhookValidator SHALL treat the WebhookSecret as empty and return an invalid result for all requests.
4. THE WebhooksController SHALL be registered in the ASP.NET Core dependency injection container alongside the ShopifyWebhookValidator and ShopifyWebhookService.
