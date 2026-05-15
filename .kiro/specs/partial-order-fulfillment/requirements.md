# Requirements Document

## Introduction

This feature adds **Partial Order Fulfillment** to the existing Shopify integration. The system currently supports fulfilling all line items of an order in a single call via `POST /api/orders/{orderId}/fulfill`. The new capability allows callers to fulfill one or more specific line items individually via `POST /api/orders/{orderId}/fulfill-items`, using Shopify's `fulfillmentCreate` mutation with `lineItemsByFulfillmentOrder`. When all line items of an order are fulfilled, the order's fulfillment status is updated accordingly. The existing full-fulfillment endpoint and all webhook handlers must continue to operate without modification.

---

## Glossary

- **System**: The ShopifyIntegration .NET application.
- **API**: The ASP.NET Core Web API layer (`OrdersController`).
- **Handler**: The MediatR command handler (`FulfillOrderLineItemsCommandHandler`).
- **Validator**: The FluentValidation validator (`FulfillOrderLineItemsCommandValidator`).
- **ShopifyService**: The `IShopifyFulfillmentService` / `ShopifyFulfillmentService` infrastructure class.
- **Order**: A Shopify order entity stored locally (`Order` domain entity).
- **LineItem**: A single product line within an order (`OrderLineItem` domain entity), identified by a local integer Id, a Shopify numeric Id, or a Shopify GID.
- **FulfillmentOrder**: A Shopify-side grouping of line items that can be fulfilled together, identified by a Shopify GID.
- **FulfillmentOrderLineItem**: A specific line item within a FulfillmentOrder, identified by a Shopify GID.
- **Fulfillment**: A record of a completed fulfillment action, stored locally (`Fulfillment` domain entity) and in Shopify.
- **PartialFulfillmentRequest**: The JSON request body for `POST /api/orders/{orderId}/fulfill-items`.
- **WebhookHandler**: The existing MediatR handlers for `orders/fulfilled` and `fulfillments/create` Shopify webhook topics.
- **GID**: A Shopify Global ID string of the form `gid://shopify/{Type}/{numericId}`.
- **CQRS**: Command Query Responsibility Segregation pattern used throughout the application.

---

## Requirements

### Requirement 1: New Partial Fulfillment API Endpoint

**User Story:** As an API consumer, I want to fulfill specific line items of an order individually, so that I can support warehouse workflows where items ship at different times.

#### Acceptance Criteria

1. THE API SHALL expose a `POST /api/orders/{orderId}/fulfill-items` endpoint that accepts a `PartialFulfillmentRequest` body.
2. THE API SHALL accept `orderId` as a path parameter that may be a local integer Id, a Shopify numeric Id, or a Shopify GID.
3. WHEN a valid `PartialFulfillmentRequest` is received and the Handler completes successfully, THE API SHALL dispatch a `FulfillOrderLineItemsCommand` via MediatR and return HTTP 202 Accepted with the fulfillment result.
4. IF the `PartialFulfillmentRequest` body is missing or malformed, THEN THE API SHALL return HTTP 400 Bad Request with a descriptive validation error message.
5. IF the `FulfillOrderLineItemsCommand` handler throws a `ShopifyFulfillmentException`, THEN THE API SHALL return an HTTP error response via the existing `GlobalExceptionMiddleware`, not HTTP 202 Accepted.

---

### Requirement 2: Partial Fulfillment Request Contract

**User Story:** As an API consumer, I want a clear and consistent request schema for partial fulfillment, so that I can integrate reliably without ambiguity.

#### Acceptance Criteria

1. THE `PartialFulfillmentRequest` SHALL contain a `lineItemIds` field that is a non-empty list of strings, where each string may be a local integer Id, a Shopify numeric Id, or a Shopify GID of an `OrderLineItem`.
2. THE `PartialFulfillmentRequest` SHALL contain an optional `trackingNumber` string field.
3. THE `PartialFulfillmentRequest` SHALL contain an optional `trackingCompany` string field.
4. THE `PartialFulfillmentRequest` SHALL contain a `notifyCustomer` boolean field that defaults to `true`.
5. THE Validator SHALL reject a `FulfillOrderLineItemsCommand` where `OrderId` is empty or whitespace, returning a validation error.
6. THE Validator SHALL reject a `FulfillOrderLineItemsCommand` where `LineItemIds` is null or empty, returning a validation error.
7. THE Validator SHALL reject a `FulfillOrderLineItemsCommand` where any entry in `LineItemIds` is an empty or whitespace string, returning a validation error.

---

### Requirement 3: Line Item ID Resolution

**User Story:** As a developer, I want the handler to resolve line item identifiers flexibly, so that callers are not forced to know the internal ID format.

#### Acceptance Criteria

1. WHEN a `lineItemId` value is a valid integer, THE Handler SHALL attempt to match it against the local `OrderLineItem.Id` (primary key) first, then against `OrderLineItem.NumericId`.
2. WHEN a `lineItemId` value starts with `gid://shopify/`, THE Handler SHALL treat it as a Shopify GID and match it against `OrderLineItem.ShopifyGid`.
3. WHEN a `lineItemId` cannot be matched to any `OrderLineItem` in the local database, THE Handler SHALL log a warning and continue processing the remaining line items without throwing immediately.
4. WHEN all provided `lineItemIds` have been processed and none resolved to a known `OrderLineItem`, THE Handler SHALL throw a `ShopifyFulfillmentException` with a descriptive message.

---

### Requirement 4: Fulfillment Order Mapping

**User Story:** As a developer, I want the handler to correctly map resolved line items to Shopify FulfillmentOrder line items, so that the Shopify `fulfillmentCreate` mutation receives the correct input.

#### Acceptance Criteria

1. WHEN the Handler resolves the order GID, THE Handler SHALL call `ShopifyService.FulfillLineItemsAsync` with the order GID, the list of resolved `OrderLineItem.ShopifyGid` values, and the tracking and notification parameters.
2. THE ShopifyService SHALL query Shopify's `fulfillmentOrders` for the given order GID and retrieve all FulfillmentOrders with status `OPEN` or `IN_PROGRESS`, including their `lineItems` edges with each line item's `id` and `lineItem { id }`.
3. WHEN mapping requested line item GIDs to FulfillmentOrder line items, THE ShopifyService SHALL match each requested `lineItemGid` against the `lineItem { id }` field of each `FulfillmentOrderLineItem`.
4. THE ShopifyService SHALL group matched `FulfillmentOrderLineItem` GIDs by their parent `FulfillmentOrder` GID and build a `lineItemsByFulfillmentOrder` input list for the `fulfillmentCreate` mutation.
5. IF no FulfillmentOrder line items match any of the requested line item GIDs, THEN THE ShopifyService SHALL throw a `ShopifyFulfillmentException` with a descriptive message, and the `fulfillmentCreate` mutation SHALL NOT be called.
6. THE ShopifyService SHALL call the `fulfillmentCreate` GraphQL mutation with the constructed `lineItemsByFulfillmentOrder` input, tracking info, and `notifyCustomer` flag only when at least one line item has been matched.

---

### Requirement 5: Local Persistence After Partial Fulfillment

**User Story:** As a developer, I want the local database to reflect partial fulfillment results, so that the local state stays consistent with Shopify.

#### Acceptance Criteria

1. WHEN Shopify returns a successful fulfillment, THE Handler SHALL persist a new `Fulfillment` entity to the local database using `IFulfillmentRepository.UpsertAsync`, linked to the local `Order.Id`.
2. THE Handler SHALL populate the `Fulfillment` entity with `ShopifyGid`, `NumericId`, `OrderId`, `Status`, `TrackingNumber`, `TrackingCompany`, `TrackingUrl`, `CreatedAt`, and `UpdatedAt` fields, following the same pattern as `FulfillOrderCommandHandler`.
3. WHEN the order is found in the local database and all of its line items are now fulfilled, THE Handler SHALL update `Order.FulfillmentStatus` to `"fulfilled"` and `Order.UpdatedAt` to the current UTC time.
4. WHEN the order is found in the local database and only some line items are fulfilled, THE Handler SHALL update `Order.FulfillmentStatus` to `"partial"` and `Order.UpdatedAt` to the current UTC time.
5. IF the order is not found in the local database, THEN THE Handler SHALL log a warning and skip local persistence without throwing an exception; system-level errors such as database connection failures SHALL still propagate as exceptions.

---

### Requirement 6: Fulfillment Status Determination

**User Story:** As a developer, I want the system to correctly determine whether an order is fully or partially fulfilled after a partial fulfillment call, so that the order status accurately reflects reality.

#### Acceptance Criteria

1. WHEN determining post-fulfillment order status, THE Handler SHALL reload the order's fulfillments from the local database after upserting the new fulfillment record.
2. THE Handler SHALL compare the count of distinct fulfilled `OrderLineItem` GIDs (derived from all local `Fulfillment` records for the order) against the total count of `OrderLineItem` records for that order.
3. WHEN all `OrderLineItem` records for the order have a corresponding fulfilled entry, THE Handler SHALL set `Order.FulfillmentStatus` to `"fulfilled"`.
4. WHEN at least one `OrderLineItem` record for the order does not have a corresponding fulfilled entry, THE Handler SHALL set `Order.FulfillmentStatus` to `"partial"`.

---

### Requirement 7: Partial Fulfillment Result Contract

**User Story:** As an API consumer, I want a consistent response from the partial fulfillment endpoint, so that I can confirm which items were fulfilled and track the shipment.

#### Acceptance Criteria

1. THE Handler SHALL return a `FulfillOrderLineItemsResult` record containing `OrderGid`, `FulfillmentGid`, `Status`, `TrackingNumber`, `TrackingCompany`, and `TrackingUrl` fields.
2. THE `FulfillOrderLineItemsResult` SHALL follow the same field structure as `FulfillOrderResult` to maintain API consistency.
3. WHEN Shopify returns tracking info as a list, THE Handler SHALL use the first entry's `Number`, `Company`, and `Url` values, consistent with the existing `FulfillOrderCommandHandler` behaviour.

---

### Requirement 8: Existing Full Fulfillment Endpoint Compatibility

**User Story:** As an API consumer, I want the existing `POST /api/orders/{orderId}/fulfill` endpoint to continue working without any changes, so that existing integrations are not broken.

#### Acceptance Criteria

1. THE API SHALL continue to expose `POST /api/orders/{orderId}/fulfill` with its existing behaviour and response contract.
2. THE `FulfillOrderCommand`, `FulfillOrderCommandHandler`, `FulfillOrderCommandValidator`, and `ShopifyFulfillmentService.FulfillOrderAsync` SHALL remain unchanged.
3. WHILE both endpoints are active, THE System SHALL route requests to `fulfill` and `fulfill-items` independently without interference.

---

### Requirement 9: Webhook Handler Compatibility

**User Story:** As a developer, I want the existing webhook handlers for `orders/fulfilled` and `fulfillments/create` to continue processing events triggered by partial fulfillments, so that the local database stays consistent regardless of how a fulfillment was created.

#### Acceptance Criteria

1. THE WebhookHandler for `fulfillments/create` SHALL process fulfillment webhook payloads created by partial fulfillments using the same logic as for full fulfillments.
2. THE WebhookHandler for `orders/fulfilled` SHALL update `Order.FulfillmentStatus` to `"fulfilled"` when Shopify sends the `orders/fulfilled` event, regardless of whether the fulfillment was partial or full.
3. THE WebhookHandler for `fulfillments/create` SHALL check idempotency when a webhook arrives using `IWebhookEventRepository.ExistsProcessedAsync` to avoid duplicate processing of the same fulfillment event.
4. THE System SHALL NOT require any modifications to `HandleFulfillmentCreatedCommandHandler` or `HandleOrderFulfilledCommandHandler` to support partial fulfillment webhooks.

---

### Requirement 10: Logging and Observability

**User Story:** As a developer, I want consistent, structured logging throughout the partial fulfillment flow, so that I can diagnose issues in production.

#### Acceptance Criteria

1. THE Handler SHALL log an informational message upon successful partial fulfillment, including `OrderGid` and `FulfillmentGid`, following the same pattern as `FulfillOrderCommandHandler`.
2. THE Handler SHALL log a warning when the order is not found in the local database, including the `OrderId` value.
3. THE Handler SHALL log a warning when a requested `lineItemId` cannot be resolved to a local `OrderLineItem`, including the unresolved `lineItemId` value.
4. THE ShopifyService SHALL log a warning when Shopify returns `userErrors` in the `fulfillmentCreate` mutation response, including the error messages.
5. THE ShopifyService SHALL log an informational message upon a successful Shopify API call, including `OrderGid` and the resulting `FulfillmentGid`.
