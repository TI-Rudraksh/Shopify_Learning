# Requirements Document

## Introduction

This feature adds Customers, Orders, and full Order Fulfillment support to the existing .NET 9 ASP.NET Core Shopify Integration API. It introduces four new domain entities (`Customer`, `Order`, `OrderLineItem`, `Fulfillment`), extends the webhook processing pipeline to handle Shopify order and fulfillment events, and exposes a REST endpoint to trigger fulfillment of an order via the Shopify GraphQL Admin API. The implementation follows the same CQRS/MediatR, EF Core, repository, and Shopify service patterns already established by the Inventory and Product features.

## Glossary

- **Order**: A Shopify order record synced into the local database, representing a customer purchase.
- **Customer**: A Shopify customer record optionally associated with an order.
- **OrderLineItem**: A single line item within an order, representing one product/variant and its quantity.
- **Fulfillment**: A Shopify fulfillment record representing a shipment created for an order, optionally including tracking information.
- **FulfillmentOrder**: A Shopify concept grouping line items that can be fulfilled together; must be fetched before calling `fulfillmentCreate`.
- **OrdersController**: The ASP.NET Core API controller exposing order-related endpoints.
- **FulfillOrderCommand**: The MediatR command that orchestrates the full fulfillment flow against Shopify and persists the result locally.
- **IShopifyFulfillmentService**: The interface abstracting Shopify GraphQL calls for fulfillment operations.
- **ShopifyFulfillmentService**: The concrete implementation of `IShopifyFulfillmentService`.
- **IOrderRepository**: Repository interface for `Order` persistence operations.
- **ICustomerRepository**: Repository interface for `Customer` persistence operations.
- **IFulfillmentRepository**: Repository interface for `Fulfillment` persistence operations.
- **ProcessShopifyWebhookCommand**: The existing MediatR command that routes incoming Shopify webhook payloads to topic-specific handlers.
- **WebhookEvent**: The existing entity used to record idempotency and audit state for processed webhooks.
- **ShopifyGidHelper**: The existing static helper for building and parsing Shopify Global IDs (GIDs).
- **ShopifyFulfillmentException**: A domain exception thrown when Shopify returns `userErrors` during a fulfillment mutation.
- **NotFoundException**: A domain exception thrown when a requested resource cannot be found locally or in Shopify.
- **GlobalExceptionMiddleware**: The existing middleware that maps domain exceptions to HTTP status codes.
- **xmin**: PostgreSQL system column used as an optimistic concurrency token in EF Core configurations.
- **snake_case**: Column naming convention used in all EF Core table and column configurations.

---

## Requirements

### Requirement 1: Domain Entities

**User Story:** As a developer, I want well-defined domain entities for Customer, Order, OrderLineItem, and Fulfillment, so that order and fulfillment data can be persisted and queried consistently.

#### Acceptance Criteria

1. THE System SHALL define a `Customer` entity with properties: `Id` (int, PK), `ShopifyGid` (string, unique), `NumericId` (long), `Email` (string), `FirstName` (string), `LastName` (string), `Phone` (string, nullable), `CreatedAt` (DateTimeOffset), `UpdatedAt` (DateTimeOffset).
2. THE System SHALL define an `Order` entity with properties: `Id` (int, PK), `ShopifyGid` (string, unique), `NumericId` (long), `OrderNumber` (string), `Email` (string), `FinancialStatus` (string), `FulfillmentStatus` (string), `TotalPrice` (decimal), `Currency` (string), `CustomerId` (int, nullable FK → Customer.Id), `CreatedAt` (DateTimeOffset), `UpdatedAt` (DateTimeOffset).
3. THE System SHALL define an `OrderLineItem` entity with properties: `Id` (int, PK), `ShopifyGid` (string, unique), `NumericId` (long), `OrderId` (int, FK → Order.Id), `Title` (string), `VariantTitle` (string), `Quantity` (int), `Price` (decimal), `Sku` (string), `ProductGid` (string), `VariantGid` (string).
4. THE System SHALL define a `Fulfillment` entity with properties: `Id` (int, PK), `ShopifyGid` (string, unique), `NumericId` (long), `OrderId` (int, FK → Order.Id), `Status` (string), `TrackingNumber` (string, nullable), `TrackingCompany` (string, nullable), `TrackingUrl` (string, nullable), `CreatedAt` (DateTimeOffset), `UpdatedAt` (DateTimeOffset).
5. THE System SHALL define an `Order` navigation property `Customer` (optional, `Customer?`) and collections `LineItems` (`ICollection<OrderLineItem>`) and `Fulfillments` (`ICollection<Fulfillment>`).

---

### Requirement 2: EF Core Infrastructure

**User Story:** As a developer, I want EF Core configurations and an updated DbContext for all new entities, so that the database schema is created and maintained correctly.

#### Acceptance Criteria

1. THE System SHALL provide an `OrderConfiguration` implementing `IEntityTypeConfiguration<Order>` that maps all `Order` properties to snake_case column names, sets `shopify_gid` as a unique index, and configures the optional FK to `Customer` and the one-to-many relationships to `OrderLineItem` and `Fulfillment`.
2. THE System SHALL provide a `CustomerConfiguration` implementing `IEntityTypeConfiguration<Customer>` that maps all `Customer` properties to snake_case column names and sets `shopify_gid` as a unique index.
3. THE System SHALL provide an `OrderLineItemConfiguration` implementing `IEntityTypeConfiguration<OrderLineItem>` that maps all `OrderLineItem` properties to snake_case column names, sets `shopify_gid` as a unique index, and configures the FK to `Order` with a named constraint.
4. THE System SHALL provide a `FulfillmentConfiguration` implementing `IEntityTypeConfiguration<Fulfillment>` that maps all `Fulfillment` properties to snake_case column names, sets `shopify_gid` as a unique index, and configures the FK to `Order` with a named constraint.
5. THE System SHALL add `DbSet<Customer>`, `DbSet<Order>`, `DbSet<OrderLineItem>`, and `DbSet<Fulfillment>` properties to `ShopifyDbContext`.
6. WHEN `ShopifyDbContext.OnModelCreating` is called, THE System SHALL apply all new entity configurations via `ApplyConfigurationsFromAssembly`.

---

### Requirement 3: Repository Interfaces and Implementations

**User Story:** As a developer, I want repository interfaces and EF Core implementations for Order, Customer, and Fulfillment, so that application-layer commands can persist and query data without depending on EF Core directly.

#### Acceptance Criteria

1. THE System SHALL define `IOrderRepository` with methods: `UpsertAsync(Order, CancellationToken)`, `GetByShopifyGidAsync(string, CancellationToken)`, `GetByNumericIdAsync(long, CancellationToken)`, `GetByIdAsync(int, CancellationToken)`, `GetAllAsync(CancellationToken)`.
2. THE System SHALL define `ICustomerRepository` with methods: `UpsertAsync(Customer, CancellationToken)`, `GetByShopifyGidAsync(string, CancellationToken)`, `GetByNumericIdAsync(long, CancellationToken)`.
3. THE System SHALL define `IFulfillmentRepository` with methods: `UpsertAsync(Fulfillment, CancellationToken)`, `GetByShopifyGidAsync(string, CancellationToken)`, `GetByOrderIdAsync(int, CancellationToken)`.
4. THE System SHALL provide `OrderRepository`, `CustomerRepository`, and `FulfillmentRepository` as sealed EF Core implementations of the respective interfaces, following the same upsert-by-ShopifyGid pattern used in `ProductRepository`.
5. THE System SHALL register `IOrderRepository`, `ICustomerRepository`, and `IFulfillmentRepository` with their implementations as scoped services in `Program.cs`.

---

### Requirement 4: Shopify Fulfillment Service

**User Story:** As a developer, I want a Shopify fulfillment service that abstracts the GraphQL calls needed to fulfill an order, so that the application layer is decoupled from Shopify API details.

#### Acceptance Criteria

1. THE System SHALL define `IShopifyFulfillmentService` with method `FulfillOrderAsync(string orderGid, string? trackingNumber, string? trackingCompany, bool notifyCustomer, CancellationToken)` returning a `FulfillmentCreateResult`.
2. THE System SHALL provide `ShopifyFulfillmentService` as a concrete implementation of `IShopifyFulfillmentService` that uses the existing `GraphService` pattern (same as `ShopifyInventoryService`).
3. WHEN `FulfillOrderAsync` is called, THE `ShopifyFulfillmentService` SHALL first query Shopify for the fulfillment orders associated with the given order GID using the `fulfillmentOrders` GraphQL query.
4. WHEN fulfillment orders are retrieved, THE `ShopifyFulfillmentService` SHALL call the `fulfillmentCreate` GraphQL mutation with the fulfillment order IDs, optional tracking details, and the `notifyCustomer` flag.
5. IF the `fulfillmentCreate` mutation returns `userErrors`, THEN THE `ShopifyFulfillmentService` SHALL throw a `ShopifyFulfillmentException` containing the error messages.
6. THE System SHALL define `ShopifyFulfillmentException` following the same pattern as `ShopifyInventoryException`, with a `ShopifyErrors` property of type `IReadOnlyList<string>`.
7. THE System SHALL register `IShopifyFulfillmentService` with `ShopifyFulfillmentService` as a scoped service in `Program.cs`.

---

### Requirement 5: GraphQL Response Models for Fulfillment

**User Story:** As a developer, I want strongly-typed GraphQL response POCOs for fulfillment queries and mutations, so that Shopify API responses can be deserialized reliably.

#### Acceptance Criteria

1. THE System SHALL define a `GetFulfillmentOrdersResponse` POCO that maps the Shopify `order { fulfillmentOrders { edges { node { id status } } } }` response shape.
2. THE System SHALL define a `FulfillmentCreateResponse` POCO that maps the Shopify `fulfillmentCreate { fulfillment { id status trackingInfo { number company url } } userErrors { field message } }` response shape.
3. THE System SHALL place all new GraphQL response POCOs under `GraphQL/Responses/Orders/` following the existing `GraphQL/Responses/Inventory/` folder convention.
4. THE System SHALL define `FulfillmentQueries` and `FulfillmentMutations` static classes containing the raw GraphQL query and mutation strings, placed under `GraphQL/Queries/` and `GraphQL/Mutations/` respectively.

---

### Requirement 6: FulfillOrderCommand — Full Fulfillment Flow

**User Story:** As an operator, I want to trigger full order fulfillment via a command, so that an order is marked as fulfilled in Shopify and the result is persisted locally.

#### Acceptance Criteria

1. THE System SHALL define `FulfillOrderCommand` as a sealed record implementing `IRequest<FulfillOrderResult>` with properties: `OrderId` (string, accepts local int ID, numeric Shopify ID, or full GID), `TrackingNumber` (string?, optional), `TrackingCompany` (string?, optional), `NotifyCustomer` (bool, default false).
2. THE System SHALL define `FulfillOrderCommandValidator` using FluentValidation that requires `OrderId` to be non-empty.
3. WHEN `FulfillOrderCommand` is handled, THE `FulfillOrderCommandHandler` SHALL resolve the order's Shopify GID by checking the local database using the provided `OrderId` (trying local int ID, then numeric ID, then treating the value as a GID directly).
4. IF the order cannot be resolved, THEN THE `FulfillOrderCommandHandler` SHALL throw a `NotFoundException` with a descriptive message.
5. WHEN the order GID is resolved, THE `FulfillOrderCommandHandler` SHALL call `IShopifyFulfillmentService.FulfillOrderAsync` with the GID and optional tracking details.
6. WHEN fulfillment succeeds, THE `FulfillOrderCommandHandler` SHALL upsert a `Fulfillment` entity into the local database via `IFulfillmentRepository`.
7. THE `FulfillOrderCommandHandler` SHALL log all fulfillment attempts at Information level regardless of success or failure, including the order GID and, when available, the fulfillment GID and any error details.
8. THE `FulfillOrderCommandHandler` SHALL return a `FulfillOrderResult` record containing the fulfillment GID, status, tracking number, tracking company, and tracking URL.

---

### Requirement 7: Order Sync Commands

**User Story:** As a developer, I want skeleton sync commands for orders and customers, so that the application layer is ready to process webhook-driven data ingestion.

#### Acceptance Criteria

1. THE System SHALL define `SyncOrderCommand` as a sealed record implementing `IRequest<Unit>` with properties sufficient to create or update an `Order` entity (NumericId, OrderNumber, Email, FinancialStatus, FulfillmentStatus, TotalPrice, Currency, UpdatedAt, and optional customer fields).
2. THE System SHALL define `SyncOrderCommandHandler` that upserts the `Order` entity via `IOrderRepository`; WHEN customer data is present in the command, THE Handler SHALL also upsert the `Customer` entity via `ICustomerRepository`.
3. THE System SHALL define `SyncCustomerCommand` as a sealed record implementing `IRequest<Unit>` with properties sufficient to create or update a `Customer` entity (NumericId, Email, FirstName, LastName, Phone, UpdatedAt).
4. THE System SHALL define `SyncCustomerCommandHandler` that upserts the `Customer` entity via `ICustomerRepository`; both the handler and `ICustomerRepository` must be defined and registered for this requirement to be satisfied.

---

### Requirement 8: Webhook Payload Models

**User Story:** As a developer, I want strongly-typed POCO models for Shopify order and fulfillment webhook payloads, so that incoming JSON can be deserialized reliably using Newtonsoft.Json.

#### Acceptance Criteria

1. THE System SHALL define `OrderWebhookPayload` with Newtonsoft.Json `[JsonProperty]` attributes mapping: `id` (long), `order_number` (string), `email` (string), `financial_status` (string), `fulfillment_status` (string, nullable), `total_price` (string), `currency` (string), `updated_at` (DateTimeOffset), and a nested `customer` object.
2. THE System SHALL define `OrderWebhookCustomer` with properties: `id` (long), `email` (string), `first_name` (string), `last_name` (string), `phone` (string, nullable).
3. THE System SHALL define `FulfillmentWebhookPayload` with Newtonsoft.Json `[JsonProperty]` attributes mapping: `id` (long), `order_id` (long), `status` (string), `tracking_number` (string, nullable), `tracking_company` (string, nullable), `tracking_url` (string, nullable), `updated_at` (DateTimeOffset).
4. THE System SHALL place all new webhook payload models under `Features/Webhooks/Models/` following the existing `ProductCreatedWebhook` convention.

---

### Requirement 9: Webhook Handlers for Order and Fulfillment Topics

**User Story:** As a developer, I want webhook handlers for `orders/create`, `orders/updated`, `orders/fulfilled`, and `fulfillments/create`, so that Shopify events are processed idempotently and persisted locally.

#### Acceptance Criteria

1. THE System SHALL define `HandleOrderCreatedCommand`, `HandleOrderUpdatedCommand`, `HandleOrderFulfilledCommand`, and `HandleFulfillmentCreatedCommand` as sealed records implementing `IRequest<Unit>`, each carrying the deserialized payload data needed to upsert the relevant entity.
2. WHEN a webhook handler processes a command, THE Handler SHALL first call `IWebhookEventRepository.ExistsProcessedAsync` with the topic and numeric ID to enforce idempotency, returning `Unit.Value` immediately if already processed.
3. WHEN the event has not been processed, THE Handler SHALL upsert the relevant entity (Order or Fulfillment) via the appropriate repository.
4. WHEN processing succeeds, THE Handler SHALL persist a `WebhookEvent` with `Status = "processed"` via `IWebhookEventRepository.AddAsync`.
5. IF an exception occurs during processing, THEN THE Handler SHALL persist a `WebhookEvent` with `Status = "failed"` and `ErrorMessage` set, then re-throw the exception.
6. THE `HandleOrderCreatedCommandHandler` and `HandleOrderUpdatedCommandHandler` SHALL upsert the `Order` entity; WHEN a customer is present in the payload, THE Handler SHALL attempt to upsert the `Customer` entity via `ICustomerRepository`; IF the customer upsert fails, THEN THE Handler SHALL log the error and continue processing the order without re-throwing the customer exception.
7. THE `HandleFulfillmentCreatedCommandHandler` SHALL upsert the `Fulfillment` entity linked to the correct `Order` by resolving the order's local ID from the order's numeric Shopify ID.

---

### Requirement 10: Webhook Router Extension

**User Story:** As a developer, I want the existing `ProcessShopifyWebhookCommandHandler` to route the four new order and fulfillment topics, so that incoming webhooks are dispatched to the correct handlers without breaking existing routing.

#### Acceptance Criteria

1. WHEN the topic is `orders/create`, THE `ProcessShopifyWebhookCommandHandler` SHALL deserialize the body as `OrderWebhookPayload` and dispatch `HandleOrderCreatedCommand`.
2. WHEN the topic is `orders/updated`, THE `ProcessShopifyWebhookCommandHandler` SHALL deserialize the body as `OrderWebhookPayload` and dispatch `HandleOrderUpdatedCommand`.
3. WHEN the topic is `orders/fulfilled`, THE `ProcessShopifyWebhookCommandHandler` SHALL deserialize the body as `OrderWebhookPayload` and dispatch `HandleOrderFulfilledCommand`.
4. WHEN the topic is `fulfillments/create`, THE `ProcessShopifyWebhookCommandHandler` SHALL deserialize the body as `FulfillmentWebhookPayload` and dispatch `HandleFulfillmentCreatedCommand`.
5. THE existing routing cases for `products/*` and `locations/*` topics SHALL remain unchanged.

---

### Requirement 11: Orders API Endpoint

**User Story:** As an operator, I want a REST endpoint to fulfill an order, so that I can trigger fulfillment programmatically without accessing Shopify directly.

#### Acceptance Criteria

1. THE System SHALL expose `POST /api/orders/{orderId}/fulfill` via `OrdersController`, accepting an optional JSON body with `trackingNumber` (string?), `trackingCompany` (string?), and `notifyCustomer` (bool, default false).
2. WHEN the fulfillment succeeds, THE `OrdersController` SHALL return HTTP 202 Accepted with the `FulfillOrderResult` as the response body.
3. IF a `NotFoundException` is thrown, THEN THE `GlobalExceptionMiddleware` SHALL return HTTP 404 Not Found.
4. IF a `ShopifyFulfillmentException` is thrown, THEN THE `GlobalExceptionMiddleware` SHALL return HTTP 502 Bad Gateway with the Shopify error messages.
5. THE `OrdersController` SHALL follow the existing controller pattern: sealed class, `IMediator` injected via constructor, no try/catch blocks (exception handling delegated to `GlobalExceptionMiddleware`).
6. THE System SHALL define `FulfillOrderRequest` as the request DTO with properties `TrackingNumber` (string?), `TrackingCompany` (string?), and `NotifyCustomer` (bool).
7. THE System SHALL define `FulfillOrderResponse` as the response DTO mirroring `FulfillOrderResult` fields: `FulfillmentGid`, `Status`, `TrackingNumber`, `TrackingCompany`, `TrackingUrl`.

---

### Requirement 12: Exception Handling for Fulfillment

**User Story:** As a developer, I want `NotFoundException` and `ShopifyFulfillmentException` to be handled by the global middleware, so that API consumers receive consistent, meaningful HTTP error responses.

#### Acceptance Criteria

1. THE System SHALL define `NotFoundException` as a sealed exception class with a `Message` property, thrown when a requested order cannot be resolved.
2. THE System SHALL define `ShopifyFulfillmentException` as a sealed exception class with a `ShopifyErrors` property of type `IReadOnlyList<string>`, following the same pattern as `ShopifyInventoryException`.
3. WHEN `GlobalExceptionMiddleware` catches a `NotFoundException`, THE Middleware SHALL return HTTP 404 Not Found with a JSON body containing the error message.
4. WHEN `GlobalExceptionMiddleware` catches a `ShopifyFulfillmentException`, THE Middleware SHALL return HTTP 502 Bad Gateway with a JSON body containing the `ShopifyErrors` list.

---

### Requirement 13: ShopifyGidHelper Extensions

**User Story:** As a developer, I want `ShopifyGidHelper` to include builder methods for Order, Customer, and Fulfillment GIDs, so that GID construction is consistent and centralized.

#### Acceptance Criteria

1. THE System SHALL add `BuildOrderGid(long numericId)` to `ShopifyGidHelper` returning `"gid://shopify/Order/{numericId}"`.
2. THE System SHALL add `BuildCustomerGid(long numericId)` to `ShopifyGidHelper` returning `"gid://shopify/Customer/{numericId}"`.
3. THE System SHALL add `BuildFulfillmentGid(long numericId)` to `ShopifyGidHelper` returning `"gid://shopify/Fulfillment/{numericId}"`.
4. THE existing `ParseNumericId`, `BuildProductGid`, `BuildLocationGid`, and `BuildInventoryItemGid` methods SHALL remain unchanged.
