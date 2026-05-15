# Requirements Document

## Introduction

This feature adds PostgreSQL database persistence to the existing Shopify integration system. Currently, product data retrieved via the Shopify GraphQL API and webhook events received from Shopify are processed in-memory and logged, but never stored. This feature introduces a database layer using Entity Framework Core with a PostgreSQL backend so that products and webhook events are durably persisted, queryable, and auditable.

The integration already has:
- `ShopifyGraphQLService` — CRUD operations against the Shopify GraphQL API
- `ShopifyWebhookService` — processes `products/create`, `products/update`, and `products/delete` webhook events
- `ProductsController` and `WebhooksController` — HTTP entry points

The database layer will be introduced without breaking any existing behaviour.

---

## Glossary

- **Database**: The PostgreSQL instance that stores persisted data for this system.
- **DbContext**: The Entity Framework Core `ShopifyDbContext` that manages database connections and entity mappings.
- **Product**: A local database record representing a Shopify product, identified by its Shopify product GID.
- **WebhookEvent**: A database record capturing a raw Shopify webhook payload together with its topic, processing status, and timestamps.
- **Repository**: A data-access class that encapsulates all database queries and commands for a given entity type.
- **IProductRepository**: The interface for product persistence operations.
- **IWebhookEventRepository**: The interface for webhook event persistence operations.
- **Migration**: An Entity Framework Core database migration that creates or alters database schema.
- **ShopifyGraphQLService**: The existing service that calls the Shopify GraphQL API.
- **ShopifyWebhookService**: The existing service that processes inbound Shopify webhook payloads.
- **ProductsController**: The existing ASP.NET Core controller that exposes product CRUD endpoints.
- **WebhooksController**: The existing ASP.NET Core controller that receives Shopify webhook HTTP calls.
- **Shopify_GID**: A Shopify Global ID string of the form `gid://shopify/Product/{numericId}`.
- **Numeric_Product_Id**: The numeric portion of a Shopify GID (e.g. `123456789`).

---

## Requirements

### Requirement 1: Database Configuration

**User Story:** As a developer, I want the application to read its PostgreSQL connection string from configuration, so that the database connection can be changed per environment without recompiling.

#### Acceptance Criteria

1. THE `DbContext` SHALL be registered in the ASP.NET Core dependency-injection container using a connection string sourced from `appsettings.json` under the key `ConnectionStrings:DefaultConnection`.
2. IF the `ConnectionStrings:DefaultConnection` configuration value is absent or empty at application startup, THEN THE application SHALL throw an `InvalidOperationException` with a descriptive message before accepting any requests.
3. THE `DbContext` SHALL use the Npgsql Entity Framework Core provider for all database operations.

---

### Requirement 2: Product Entity and Schema

**User Story:** As a developer, I want a `products` table in the database, so that product data from Shopify can be stored and queried locally.

#### Acceptance Criteria

1. THE `Database` SHALL contain a `products` table with the following columns: `id` (integer, primary key, auto-increment), `shopify_gid` (text, unique, not null), `numeric_id` (bigint, not null), `title` (text, not null), `vendor` (text, not null), `status` (text, not null), `created_at` (timestamptz, not null), `updated_at` (timestamptz, not null).
2. THE `DbContext` SHALL enforce a unique index on `products.shopify_gid`.
3. THE `Migration` SHALL create the `products` table and its unique index when applied to an empty database.

---

### Requirement 3: Webhook Event Entity and Schema

**User Story:** As a developer, I want a `webhook_events` table in the database, so that every inbound Shopify webhook is recorded for auditing and reprocessing.

#### Acceptance Criteria

1. THE `Database` SHALL contain a `webhook_events` table with the following columns: `id` (integer, primary key, auto-increment), `topic` (text, not null), `shopify_numeric_id` (bigint, nullable), `raw_payload` (text, not null), `processed_at` (timestamptz, not null), `status` (text, not null), `error_message` (text, nullable).
2. THE `Migration` SHALL create the `webhook_events` table when applied to an empty database.

---

### Requirement 4: Product Repository

**User Story:** As a developer, I want a repository abstraction for product persistence, so that the service layer is decoupled from direct EF Core usage and is testable.

#### Acceptance Criteria

1. THE `IProductRepository` SHALL expose the following operations: `UpsertAsync(Product product)`, `GetByShopifyGidAsync(string shopifyGid)`, `GetByNumericIdAsync(long numericId)`, `DeleteByNumericIdAsync(long numericId)`, and `GetAllAsync()`.
2. WHEN `UpsertAsync` is called with a `Product` whose `ShopifyGid` does not exist in the database, THE `IProductRepository` SHALL insert a new row and return the saved entity.
3. WHEN `UpsertAsync` is called with a `Product` whose `ShopifyGid` already exists in the database, THE `IProductRepository` SHALL update the existing row's `Title`, `Vendor`, `Status`, and `UpdatedAt` fields and return the updated entity.
4. WHEN `DeleteByNumericIdAsync` is called with a `Numeric_Product_Id` that exists in the database, THE `IProductRepository` SHALL remove the corresponding row and return `true`.
5. IF `DeleteByNumericIdAsync` is called with a `Numeric_Product_Id` that does not exist in the database, THEN THE `IProductRepository` SHALL return `false` without throwing an exception.

---

### Requirement 5: Webhook Event Repository

**User Story:** As a developer, I want a repository for webhook event persistence, so that every processed webhook is durably recorded.

#### Acceptance Criteria

1. THE `IWebhookEventRepository` SHALL expose the following operations: `AddAsync(WebhookEvent webhookEvent)` and `GetByTopicAsync(string topic)`.
2. WHEN `AddAsync` is called with a valid `WebhookEvent`, THE `IWebhookEventRepository` SHALL insert the record and return the saved entity with its generated `id` populated.

---

### Requirement 6: Persist Products on GraphQL API Operations

**User Story:** As a developer, I want product data to be saved to the database whenever a product is created, updated, or deleted via the GraphQL API, so that the local database stays in sync with Shopify.

#### Acceptance Criteria

1. WHEN `ShopifyGraphQLService.CreateProductAsync` succeeds and returns a non-null product, THE `ShopifyGraphQLService` SHALL persist the product to the database via `IProductRepository.UpsertAsync` before returning the response to the caller.
2. WHEN `ShopifyGraphQLService.UpdateProductAsync` succeeds and returns a non-null product, THE `ShopifyGraphQLService` SHALL update the product in the database via `IProductRepository.UpsertAsync` before returning the response to the caller.
3. WHEN `ShopifyGraphQLService.DeleteProductAsync` succeeds, THE `ShopifyGraphQLService` SHALL remove the product from the database via `IProductRepository.DeleteByNumericIdAsync` before returning the response to the caller.
4. IF a database operation in `ShopifyGraphQLService` fails, THEN THE `ShopifyGraphQLService` SHALL log the error and propagate the exception to the caller.

---

### Requirement 7: Persist Webhook Events and Sync Products on Webhook Receipt

**User Story:** As a developer, I want every inbound Shopify webhook to be recorded in the database and the local product table to be kept in sync, so that the system has a complete audit trail and consistent product state.

#### Acceptance Criteria

1. WHEN `ShopifyWebhookService.ProcessAsync` is called with topic `products/create`, THE `ShopifyWebhookService` SHALL upsert the product into the `products` table via `IProductRepository.UpsertAsync` and record a `WebhookEvent` row with `status = "processed"` via `IWebhookEventRepository.AddAsync`.
2. WHEN `ShopifyWebhookService.ProcessAsync` is called with topic `products/update`, THE `ShopifyWebhookService` SHALL upsert the product into the `products` table via `IProductRepository.UpsertAsync` and record a `WebhookEvent` row with `status = "processed"` via `IWebhookEventRepository.AddAsync`.
3. WHEN `ShopifyWebhookService.ProcessAsync` is called with topic `products/delete`, THE `ShopifyWebhookService` SHALL delete the product from the `products` table via `IProductRepository.DeleteByNumericIdAsync` and record a `WebhookEvent` row with `status = "processed"` via `IWebhookEventRepository.AddAsync`.
4. IF a database operation inside `ShopifyWebhookService.ProcessAsync` throws an exception, THEN THE `ShopifyWebhookService` SHALL record a `WebhookEvent` row with `status = "failed"` and `error_message` set to the exception message, and return `WebhookResult.Failure`.
5. WHEN `ShopifyWebhookService.ProcessAsync` is called with an unrecognised topic, THE `ShopifyWebhookService` SHALL record a `WebhookEvent` row with `status = "skipped"` and return `WebhookResult.Success`.

---

### Requirement 8: Database Migration on Startup

**User Story:** As a developer, I want pending EF Core migrations to be applied automatically when the application starts, so that the database schema is always up to date without manual intervention.

#### Acceptance Criteria

1. WHEN the application starts, THE application SHALL apply all pending EF Core migrations to the `Database` before the HTTP server begins accepting requests.
2. IF applying migrations fails at startup, THEN THE application SHALL log the error and terminate with a non-zero exit code.

---

### Requirement 9: Connection Resilience

**User Story:** As a developer, I want the database connection to retry on transient failures, so that brief network interruptions do not cause permanent errors.

#### Acceptance Criteria

1. THE `DbContext` SHALL be configured with an Npgsql execution strategy that retries up to 3 times on transient PostgreSQL errors, with an exponential back-off delay not exceeding 30 seconds per attempt.
2. IF all retry attempts are exhausted without success, THEN THE `DbContext` SHALL propagate the final exception to the caller.
