# Implementation Plan: Inventory Update

## Overview

Implement inventory management for the Shopify integration following the existing CQRS + Vertical Slices pattern. The work is ordered from the foundational domain layer upward through infrastructure, application, and API layers, ensuring each layer can be built and tested independently before the next depends on it.

## Tasks

- [x] 1. Extend `ShopifyGidHelper` with inventory GID factory methods
  - Add `BuildLocationGid(long numericId)` and `BuildInventoryItemGid(long numericId)` static methods to `ShopifyIntegration/Infrastructure/Data/Helpers/ShopifyGidHelper.cs`, following the same pattern as the existing `BuildProductGid`
  - _Requirements: 3.5, 5.3, 5.4_

  - [ ]* 1.1 Write property test for GID construction round-trip
    - **Property 4: GID construction round-trip**
    - For any positive `long` value, assert `ParseNumericId(BuildProductGid(id)) == id`, `ParseNumericId(BuildLocationGid(id)) == id`, and `ParseNumericId(BuildInventoryItemGid(id)) == id`
    - Use FsCheck `Prop.ForAll` with a `PositiveInt` generator; tag with `// Feature: inventory-update, Property 4: GID construction round-trip`
    - **Validates: Requirements 3.5, 5.3, 5.4**

- [x] 2. Create `InventoryLevel` and `Location` domain entities
  - Create `ShopifyIntegration/Domain/Entities/InventoryLevel.cs` with properties: `Id` (int PK), `ProductId` (int FK), `Product` (navigation), `LocationGid` (string), `InventoryItemGid` (string), `Quantity` (int), `Available` (bool), `CreatedAt` (DateTimeOffset), `UpdatedAt` (DateTimeOffset), `RowVersion` (byte[])
  - Create `ShopifyIntegration/Domain/Entities/Location.cs` with properties: `Id` (int PK), `LocationGid` (string), `NumericId` (long), `Name` (string), `CreatedAt` (DateTimeOffset), `UpdatedAt` (DateTimeOffset)
  - _Requirements: 1.1, 1.2, 1.4, 2.1, 2.2_

- [x] 3. Create EF Core configurations for `InventoryLevel` and `Location`
  - Create `ShopifyIntegration/Infrastructure/Data/Configurations/InventoryLevelConfiguration.cs` mapping to `inventory_levels` table with snake_case column names, FK to `products.id`, unique index on `(product_id, location_gid)`, and `IsRowVersion()` on `RowVersion` — follow the style of `ProductConfiguration`
  - Create `ShopifyIntegration/Infrastructure/Data/Configurations/LocationConfiguration.cs` mapping to `locations` table with snake_case column names and unique index on `location_gid`
  - Add `DbSet<InventoryLevel> InventoryLevels` and `DbSet<Location> Locations` properties to `ShopifyIntegration/Infrastructure/Data/ShopifyDbContext.cs`
  - _Requirements: 1.3, 6.1, 6.2, 6.3, 6.4_

  - [ ]* 3.1 Write property test for `InventoryLevel` unique constraint enforcement
    - **Property 6: InventoryLevel unique constraint is enforced**
    - Using an in-memory or SQLite EF Core context, generate arbitrary `(ProductId, LocationGid)` pairs and attempt to insert two `InventoryLevel` records with the same pair; assert the second insert throws `DbUpdateException`
    - Tag with `// Feature: inventory-update, Property 6: InventoryLevel unique constraint is enforced`
    - **Validates: Requirements 1.3**

- [x] 4. Generate and verify EF Core migration for new tables
  - Run `dotnet ef migrations add AddInventoryLevelAndLocation` to generate a migration that adds `inventory_levels` and `locations` tables
  - Verify the generated migration file does not modify the existing `products` or `webhook_events` tables
  - _Requirements: 6.7_

- [x] 5. Create repository interfaces and implementations for `InventoryLevel` and `Location`
  - Create `ShopifyIntegration/Domain/Repositories/IInventoryRepository.cs` with methods: `UpsertAsync`, `GetByProductAndLocationAsync`, `GetAllForProductAsync`
  - Create `ShopifyIntegration/Domain/Repositories/ILocationRepository.cs` with methods: `UpsertAsync`, `GetByGidAsync`
  - Create `ShopifyIntegration/Infrastructure/Data/Repositories/InventoryRepository.cs` implementing `IInventoryRepository` using `ShopifyDbContext`, following the same pattern as `ProductRepository`
  - Create `ShopifyIntegration/Infrastructure/Data/Repositories/LocationRepository.cs` implementing `ILocationRepository` using `ShopifyDbContext`
  - _Requirements: 6.5, 6.6_

  - [ ]* 5.1 Write property test for inventory upsert persisting correct state
    - **Property 5: Inventory upsert persists correct state**
    - Generate arbitrary valid `InventoryLevel` values (non-empty GIDs, non-negative quantity) using FsCheck; call `UpsertAsync` on an in-memory/SQLite context; assert `GetByProductAndLocationAsync` returns a record with matching `Quantity`, `Available`, and `InventoryItemGid`, and `UpdatedAt >= timestamp before upsert`
    - Tag with `// Feature: inventory-update, Property 5: Inventory upsert persists correct state`
    - **Validates: Requirements 1.5, 4.7, 4.10**

- [x] 6. Create GraphQL query and mutation string constants for inventory operations
  - Create `ShopifyIntegration/GraphQL/Queries/InventoryQueries.cs` with the `GetInventoryItemGid` query constant
  - Create `ShopifyIntegration/GraphQL/Mutations/InventoryMutations.cs` with the `SetOnHandQuantities` mutation constant
  - _Requirements: 3.1, 3.2_

- [x] 7. Create GraphQL response POCOs for inventory operations
  - Create `ShopifyIntegration/GraphQL/Responses/Inventory/` folder with:
    - `GetInventoryItemGidResponse.cs` containing `GetInventoryItemGidResponse`, `ShopifyProductVariants`, `ShopifyVariantConnection`, `ShopifyVariantEdge`, `ShopifyVariantNode`, `ShopifyInventoryItemRef`
    - `SetOnHandQuantityResponse.cs` containing `SetOnHandQuantityResponse`, `InventorySetOnHandPayload`, `InventoryAdjustmentGroup`, `InventoryChange`, `ShopifyLocationRef`, `InventoryUserError`
  - _Requirements: 3.1, 3.2_

- [x] 8. Create `ShopifyInventoryException` domain exception
  - Create `ShopifyIntegration/Infrastructure/Shopify/ShopifyInventoryException.cs` as a sealed exception class with a `ShopifyErrors` (`IReadOnlyList<string>`) property, constructed from an `IEnumerable<string>` of error messages
  - _Requirements: 3.3_

- [x] 9. Create `IShopifyInventoryService` interface and `ShopifyInventoryService` implementation
  - Create `ShopifyIntegration/Infrastructure/Shopify/IShopifyInventoryService.cs` with `GetInventoryItemGidAsync(string productGid, CancellationToken ct)` and `SetOnHandQuantityAsync(string inventoryItemGid, string locationGid, int quantity, CancellationToken ct)` methods
  - Create `ShopifyIntegration/Infrastructure/Shopify/ShopifyInventoryService.cs` implementing the interface, injecting `IConfiguration` and `ILogger<ShopifyInventoryService>`, reusing the `GraphService` pattern from `ShopifyGraphQLService`
  - When the Shopify response contains `userErrors`, throw `ShopifyInventoryException` with all error messages; propagate network/HTTP exceptions unchanged
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [ ]* 9.1 Write property test for Shopify userErrors full propagation
    - **Property 3: Shopify userErrors are fully propagated**
    - Generate arbitrary non-empty lists of `InventoryUserError` objects with random `Message` strings using FsCheck; assert the `ShopifyInventoryException` thrown by the service contains every message from the input list — no message is dropped or truncated
    - Tag with `// Feature: inventory-update, Property 3: Shopify userErrors are fully propagated`
    - **Validates: Requirements 3.3**

- [x] 10. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Create `UpdateInventoryCommand`, `UpdateInventoryResult`, and `UpdateInventoryCommandValidator`
  - Create `ShopifyIntegration/Features/Inventory/Commands/UpdateInventoryCommand.cs` as a `sealed record` with `ProductGid` (string), `LocationGid` (string), `Quantity` (int), `Available` (bool, default `true`), implementing `IRequest<UpdateInventoryResult?>`
  - Create `UpdateInventoryResult` as a `sealed record` with `ProductGid`, `LocationGid`, `InventoryItemGid`, `Quantity`, `Available`, `UpdatedAt` in the same file or a companion file
  - Create `ShopifyIntegration/Features/Inventory/Commands/UpdateInventoryCommandValidator.cs` using FluentValidation: `ProductGid` not empty, `LocationGid` not empty, `Quantity >= 0`
  - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [ ]* 11.1 Write property test for validator rejecting invalid GID fields
    - **Property 1: Validator rejects invalid GID fields**
    - Generate arbitrary null, empty, or whitespace-only strings for `ProductGid` and `LocationGid` using FsCheck; assert `UpdateInventoryCommandValidator.Validate()` returns at least one failure and the command is not dispatched
    - Tag with `// Feature: inventory-update, Property 1: Validator rejects invalid GID fields`
    - **Validates: Requirements 4.2, 4.3**

  - [ ]* 11.2 Write property test for validator rejecting negative quantities
    - **Property 2: Validator rejects negative quantities**
    - Generate arbitrary negative integers for `Quantity` using FsCheck; assert `UpdateInventoryCommandValidator.Validate()` returns a failure on the `Quantity` field
    - Tag with `// Feature: inventory-update, Property 2: Validator rejects negative quantities`
    - **Validates: Requirements 4.4**

- [x] 12. Create `UpdateInventoryCommandHandler`
  - Create `ShopifyIntegration/Features/Inventory/Commands/UpdateInventoryCommandHandler.cs` implementing `IRequestHandler<UpdateInventoryCommand, UpdateInventoryResult?>`
  - Inject `IProductRepository`, `IShopifyInventoryService`, `IInventoryRepository`, `ILocationRepository`, and `ILogger<UpdateInventoryCommandHandler>`
  - Implement the orchestration steps:
    1. Look up `Product` by `ProductGid` via `IProductRepository.GetByShopifyGidAsync`; return `null` if not found
    2. Call `IShopifyInventoryService.GetInventoryItemGidAsync(productGid)` to resolve `InventoryItemGid`
    3. Call `IShopifyInventoryService.SetOnHandQuantityAsync(inventoryItemGid, locationGid, quantity)`
    4. Upsert `InventoryLevel` via `IInventoryRepository.UpsertAsync`; on `DbUpdateConcurrencyException`, retry once before propagating
    5. Upsert `Location` via `ILocationRepository.UpsertAsync`
    6. Log success at `Information` level with `InventoryItemGid`, `LocationGid`, and `Quantity`
    7. Return `UpdateInventoryResult`
  - Log Shopify `userErrors` at `Warning` level before throwing (handled by `ShopifyInventoryService`, but handler should log context)
  - _Requirements: 4.5, 4.6, 4.7, 4.8, 4.9, 4.10, 7.2, 7.4_

- [x] 13. Extend `GlobalExceptionMiddleware` to handle `ShopifyInventoryException`
  - Add a `catch (ShopifyInventoryException ex)` block in `ShopifyIntegration/Middleware/GlobalExceptionMiddleware.cs` that returns `502 Bad Gateway` with `{ errors: ex.ShopifyErrors }` as JSON, placed before the generic `catch (Exception ex)` block
  - _Requirements: 5.8, 7.3_

- [x] 14. Create `UpdateInventoryRequest` DTO and `InventoryController`
  - Create `ShopifyIntegration/DTOs/UpdateInventoryRequest.cs` with `Quantity` (int) and `Available` (bool, default `true`) properties
  - Create `ShopifyIntegration/API/Controllers/InventoryController.cs` with route `api/stores/{storeId}/products/{productId}/inventory`, a `PATCH` action that constructs `LocationGid` and `ProductGid` using `ShopifyGidHelper`, dispatches `UpdateInventoryCommand` via `IMediator`, returns `202 Accepted` with the result body, or `404 Not Found` if the result is `null`
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7_

- [x] 15. Register new services in the DI container
  - In `ShopifyIntegration/Program.cs`, register `IShopifyInventoryService` → `ShopifyInventoryService`, `IInventoryRepository` → `InventoryRepository`, and `ILocationRepository` → `LocationRepository` with the appropriate lifetimes (scoped), consistent with how `IShopifyGraphQLService` and `IProductRepository` are registered
  - _Requirements: 4.5, 4.6, 4.7, 5.2_

- [x] 16. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for a faster MVP
- Each task references specific requirements for traceability
- Property-based tests use FsCheck with a minimum of 100 iterations per property; each test is tagged with `// Feature: inventory-update, Property N: {description}`
- Unit tests and property tests are complementary — unit tests cover specific examples and edge cases, property tests verify universal invariants
- The EF Core migration (task 4) must be verified manually to confirm it does not touch existing tables
- The `LoggingBehaviour` pipeline behaviour already handles command-level correlation ID logging (Requirement 7.1) — no changes needed to that file
