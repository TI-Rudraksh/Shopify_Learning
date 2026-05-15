# Requirements Document

## Introduction

This feature adds inventory management capabilities to the existing Shopify integration. Products are global across the system, but inventory quantities are location-specific — matching Shopify's own model where an `InventoryLevel` ties an `InventoryItem` to a `Location`. The feature exposes a public API endpoint that allows callers to update the inventory quantity (and availability) for a given product at a given store location. The update must be reflected both in the local database and in Shopify via the GraphQL Admin API, using ShopifyGids throughout.

The implementation follows the existing CQRS + Vertical Slices pattern (MediatR, FluentValidation, EF Core, `IShopifyGraphQLService`) already established for the Products feature.

---

## Glossary

- **InventoryLevel**: A domain entity that records the quantity and availability of a product's inventory item at a specific location. Maps to Shopify's `InventoryLevel` (keyed by `inventory_item_id` + `location_id`).
- **InventoryItem**: Shopify's trackable unit of stock. Each product variant has exactly one `InventoryItem`. For single-variant products the `InventoryItem` is the product's sole variant's inventory item.
- **Location**: A physical or virtual place (store, warehouse) in Shopify that holds inventory. Identified by a `LocationGid` (`gid://shopify/Location/{id}`).
- **ProductGid**: A Shopify Global ID for a product, e.g. `gid://shopify/Product/{numericId}`.
- **LocationGid**: A Shopify Global ID for a location, e.g. `gid://shopify/Location/{numericId}`.
- **InventoryItemGid**: A Shopify Global ID for an inventory item, e.g. `gid://shopify/InventoryItem/{numericId}`.
- **UpdateInventoryCommand**: The MediatR command that carries the intent to update inventory for a product at a location.
- **InventoryCommandHandler**: The MediatR handler that validates, calls Shopify, and persists the result.
- **IShopifyInventoryService**: Extension of `IShopifyGraphQLService` (or a dedicated interface) responsible for inventory-related Shopify GraphQL calls.
- **InventoryRepository**: Repository interface for reading and writing `InventoryLevel` entities.
- **Quantity**: A non-negative integer representing the number of units available at a location.
- **Available**: A boolean flag indicating whether the inventory item is available for sale at the location.
- **RowVersion**: An EF Core concurrency token used for optimistic locking on `InventoryLevel`.

---

## Requirements

### Requirement 1: Domain Model — InventoryLevel Entity

**User Story:** As a developer, I want a domain entity that represents inventory at a specific location, so that inventory data can be persisted and queried independently of the product entity.

#### Acceptance Criteria

1. THE `InventoryLevel` entity SHALL have a surrogate integer primary key (`Id`).
2. THE `InventoryLevel` entity SHALL store `ProductId` (FK to `Product.Id`), `LocationGid` (string, not null), `InventoryItemGid` (string, not null), `Quantity` (int, not null), `Available` (bool, not null), `CreatedAt` (DateTimeOffset), `UpdatedAt` (DateTimeOffset), and a `RowVersion` concurrency token (byte array).
3. THE `InventoryLevel` entity SHALL enforce a unique constraint on the combination of `ProductId` and `LocationGid`, so that each product has at most one inventory record per location.
4. WHEN a new `InventoryLevel` is created, THE `InventoryLevel` entity SHALL set `CreatedAt` and `UpdatedAt` to the current UTC time.
5. WHEN an existing `InventoryLevel` is updated, THE `InventoryLevel` entity SHALL update `UpdatedAt` to the current UTC time.

---

### Requirement 2: Domain Model — Location Entity

**User Story:** As a developer, I want a domain entity that represents a Shopify location, so that location metadata can be stored and referenced without repeated Shopify API calls.

#### Acceptance Criteria

1. THE `Location` entity SHALL have a surrogate integer primary key (`Id`).
2. THE `Location` entity SHALL store `LocationGid` (string, unique, not null), `NumericId` (long, not null), `Name` (string, not null), `CreatedAt` (DateTimeOffset), and `UpdatedAt` (DateTimeOffset).
3. WHEN a `Location` is referenced during an inventory update and does not exist in the local database, THE `InventoryCommandHandler` SHALL create a `Location` record using the data returned from Shopify.

---

### Requirement 3: Shopify Inventory GraphQL Operations

**User Story:** As a developer, I want the Shopify service to support inventory-specific GraphQL mutations and queries, so that inventory can be read from and written to Shopify.

#### Acceptance Criteria

1. THE `IShopifyInventoryService` SHALL expose a method to retrieve the `InventoryItemGid` for a given `ProductGid` by querying the product's first variant from Shopify.
2. THE `IShopifyInventoryService` SHALL expose a method to call the Shopify `inventorySetOnHandQuantities` mutation, accepting `InventoryItemGid`, `LocationGid`, and `Quantity`.
3. WHEN the Shopify GraphQL response contains `userErrors`, THE `IShopifyInventoryService` SHALL throw a domain exception that includes all error messages from the response.
4. IF the Shopify API call fails due to a network or HTTP error, THEN THE `IShopifyInventoryService` SHALL propagate the exception so the caller can handle it.
5. THE `IShopifyInventoryService` SHALL use `ShopifyGid` values (not numeric IDs) in all GraphQL variables sent to Shopify.

---

### Requirement 4: Application Layer — UpdateProductInventoryCommand

**User Story:** As a developer, I want a MediatR command and handler that orchestrates the inventory update, so that the operation is consistent with the existing CQRS pattern.

#### Acceptance Criteria

1. THE `UpdateInventoryCommand` SHALL carry `ProductGid` (string), `LocationGid` (string), `Quantity` (int), and `Available` (bool, optional, default `true`).
2. THE `UpdateInventoryCommandValidator` SHALL reject commands where `ProductGid` is empty or null.
3. THE `UpdateInventoryCommandValidator` SHALL reject commands where `LocationGid` is empty or null.
4. THE `UpdateInventoryCommandValidator` SHALL reject commands where `Quantity` is less than zero.
5. WHEN the `UpdateInventoryCommand` is handled, THE `InventoryCommandHandler` SHALL resolve the `InventoryItemGid` for the given `ProductGid` by calling `IShopifyInventoryService`.
6. WHEN the `InventoryItemGid` is resolved, THE `InventoryCommandHandler` SHALL call `IShopifyInventoryService` to set the on-hand quantity in Shopify.
7. WHEN the Shopify call succeeds, THE `InventoryCommandHandler` SHALL upsert the `InventoryLevel` record in the local database with the new `Quantity`, `Available`, `InventoryItemGid`, and updated `UpdatedAt`.
8. IF the product does not exist in the local database, THEN THE `InventoryCommandHandler` SHALL return a not-found result without calling Shopify.
9. IF a concurrency conflict is detected during the database upsert, THEN THE `InventoryCommandHandler` SHALL retry the upsert once before propagating the exception.
10. THE `InventoryCommandHandler` SHALL return an `UpdateInventoryResult` containing `ProductGid`, `LocationGid`, `InventoryItemGid`, `Quantity`, `Available`, and `UpdatedAt`.

---

### Requirement 5: API Layer — Inventory Endpoint

**User Story:** As an API consumer, I want a REST endpoint to update inventory for a product at a specific store location, so that I can manage stock levels programmatically.

#### Acceptance Criteria

1. THE `InventoryController` SHALL expose a `PATCH /api/stores/{storeId}/products/{productId}/inventory` endpoint.
2. WHEN a valid request is received, THE `InventoryController` SHALL dispatch an `UpdateInventoryCommand` via MediatR and return `202 Accepted` with the `UpdateInventoryResult` in the response body.
3. THE `InventoryController` SHALL accept `storeId` as a route parameter representing the numeric Shopify location ID, and SHALL construct the `LocationGid` from it using `ShopifyGidHelper`.
4. THE `InventoryController` SHALL accept `productId` as a route parameter representing the numeric Shopify product ID, and SHALL construct the `ProductGid` from it using `ShopifyGidHelper`.
5. THE request body SHALL be an `UpdateInventoryRequest` DTO containing `Quantity` (int, required) and `Available` (bool, optional, default `true`).
6. IF the product is not found, THEN THE `InventoryController` SHALL return `404 Not Found`.
7. IF validation fails, THEN THE `InventoryController` SHALL return `400 Bad Request` with a structured error body consistent with the existing `ValidationBehaviour` response format.
8. IF a Shopify API error occurs, THEN THE `InventoryController` SHALL return `502 Bad Gateway` with a descriptive error message.

---

### Requirement 6: Infrastructure — EF Core Persistence

**User Story:** As a developer, I want EF Core configurations and repository implementations for the new entities, so that inventory data is persisted correctly in the database.

#### Acceptance Criteria

1. THE `InventoryLevelConfiguration` SHALL map `InventoryLevel` to a `inventory_levels` table using snake_case column names consistent with the existing `ProductConfiguration` style.
2. THE `InventoryLevelConfiguration` SHALL configure `RowVersion` as a concurrency token using `IsRowVersion()`.
3. THE `LocationConfiguration` SHALL map `Location` to a `locations` table using snake_case column names.
4. THE `ShopifyDbContext` SHALL include `DbSet<InventoryLevel>` and `DbSet<Location>` properties.
5. THE `IInventoryRepository` SHALL expose `UpsertAsync`, `GetByProductAndLocationAsync`, and `GetAllForProductAsync` methods.
6. THE `InventoryRepository` implementation SHALL use `ShopifyDbContext` directly, following the same pattern as `ProductRepository`.
7. WHEN an EF Core migration is required, THE migration SHALL be generated and applied without modifying existing `products` or `webhook_events` tables.

---

### Requirement 7: Error Handling and Observability

**User Story:** As a developer, I want consistent error handling and structured logging throughout the inventory update flow, so that failures are diagnosable in production.

#### Acceptance Criteria

1. WHEN the `InventoryCommandHandler` starts processing, THE `LoggingBehaviour` pipeline SHALL log the command name and a correlation ID at `Information` level, consistent with the existing `LoggingBehaviour`.
2. WHEN a Shopify `userErrors` response is received, THE `InventoryCommandHandler` SHALL log the errors at `Warning` level before throwing.
3. WHEN an unhandled exception propagates from the handler, THE `GlobalExceptionMiddleware` SHALL catch it and return a structured error response, consistent with existing middleware behaviour.
4. WHEN the inventory update succeeds, THE `InventoryCommandHandler` SHALL log the updated `InventoryItemGid`, `LocationGid`, and `Quantity` at `Information` level.
