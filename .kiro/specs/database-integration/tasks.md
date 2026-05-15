# Implementation Plan: Database Integration

## Overview

Introduce a PostgreSQL persistence layer to the Shopify integration application using Entity Framework Core (Npgsql provider) and the repository pattern. Tasks proceed from infrastructure setup through data layer creation, service updates, and finally testing — each step building on the previous so no code is left unintegrated.

## Tasks

- [x] 1. Add NuGet packages to both projects
  - Add `Npgsql.EntityFrameworkCore.PostgreSQL` to `ShopifyIntegration/ShopifyIntegration.csproj`
  - Add `Microsoft.EntityFrameworkCore.Design` (with `PrivateAssets=all`) to `ShopifyIntegration/ShopifyIntegration.csproj` for EF Core tooling
  - Add `Microsoft.EntityFrameworkCore.InMemory` to `ShopifyIntegration.Tests/ShopifyIntegration.Tests.csproj` for in-memory unit tests
  - Add `NSubstitute` to `ShopifyIntegration.Tests/ShopifyIntegration.Tests.csproj` for mocking repository interfaces in service-layer tests
  - _Requirements: 1.3, 9.1_

- [x] 2. Create data layer — entities, configurations, DbContext, repositories, and GID helper
  - [x] 2.1 Create `Product` entity and its EF Core configuration
    - Create `ShopifyIntegration/Data/Entities/Product.cs` with properties: `Id`, `ShopifyGid`, `NumericId`, `Title`, `Vendor`, `Status`, `CreatedAt`, `UpdatedAt`
    - Create `ShopifyIntegration/Data/Configurations/ProductConfiguration.cs` implementing `IEntityTypeConfiguration<Product>` — map to table `products`, snake_case column names, unique index on `shopify_gid`, `UseIdentityAlwaysColumn()` for `id`
    - _Requirements: 2.1, 2.2_

  - [x] 2.2 Create `WebhookEvent` entity and its EF Core configuration
    - Create `ShopifyIntegration/Data/Entities/WebhookEvent.cs` with properties: `Id`, `Topic`, `ShopifyNumericId` (nullable), `RawPayload`, `ProcessedAt`, `Status`, `ErrorMessage` (nullable)
    - Create `ShopifyIntegration/Data/Configurations/WebhookEventConfiguration.cs` implementing `IEntityTypeConfiguration<WebhookEvent>` — map to table `webhook_events`, snake_case column names, `UseIdentityAlwaysColumn()` for `id`
    - _Requirements: 3.1_

  - [x] 2.3 Create `ShopifyDbContext`
    - Create `ShopifyIntegration/Data/ShopifyDbContext.cs` with `DbSet<Product> Products` and `DbSet<WebhookEvent> WebhookEvents`
    - Call `modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShopifyDbContext).Assembly)` in `OnModelCreating`
    - _Requirements: 1.1, 1.3, 2.1, 3.1_

  - [x] 2.4 Create `ShopifyGidHelper`
    - Create `ShopifyIntegration/Data/Helpers/ShopifyGidHelper.cs` with static methods `ParseNumericId(string gid)` and `BuildProductGid(long numericId)`
    - `ParseNumericId` must throw `FormatException` for malformed GIDs
    - _Requirements: 6.3_

  - [ ]* 2.5 Write property test for `ShopifyGidHelper` round-trip (Property 12)
    - Create `ShopifyIntegration.Tests/DatabaseIntegration/ShopifyGidHelperTests.cs`
    - **Property 12: `ParseNumericId(BuildProductGid(n)) == n` for any positive `long`**
    - Generate random positive `long` values with FsCheck; assert round-trip equality
    - Also add a unit test verifying `ParseNumericId` throws `FormatException` for malformed input
    - Tag: `// Feature: database-integration, Property 12: GID round-trip`
    - **Validates: Requirements 6.3**

  - [x] 2.6 Create `IProductRepository` interface and `ProductRepository` implementation
    - Create `ShopifyIntegration/Data/Repositories/IProductRepository.cs` with methods: `UpsertAsync`, `GetByShopifyGidAsync`, `GetByNumericIdAsync`, `DeleteByNumericIdAsync`, `GetAllAsync`
    - Create `ShopifyIntegration/Data/Repositories/ProductRepository.cs` implementing `IProductRepository` using `ShopifyDbContext`
    - `UpsertAsync`: look up by `ShopifyGid`; insert if absent, update `Title`/`Vendor`/`Status`/`UpdatedAt` if present; call `SaveChangesAsync`
    - `DeleteByNumericIdAsync`: use `ExecuteDeleteAsync`; return `true` if rows > 0, `false` otherwise
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [ ]* 2.7 Write property tests for `ProductRepository` (Properties 1–4)
    - Create `ShopifyIntegration.Tests/DatabaseIntegration/ProductRepositoryTests.cs`
    - Use `Microsoft.EntityFrameworkCore.InMemory` provider to create a real `ShopifyDbContext` in each test
    - **Property 1: UpsertAsync inserts a new product** — generate random `Product` with unique `ShopifyGid`; assert returned `Id > 0` and all fields match input
    - **Property 2: UpsertAsync updates mutable fields** — insert a product, then upsert with modified `Title`/`Vendor`/`Status`/`UpdatedAt`; assert mutable fields updated, `Id`/`ShopifyGid`/`NumericId`/`CreatedAt` unchanged
    - **Property 3: DeleteByNumericIdAsync returns true and removes product** — insert a product, delete by `NumericId`; assert returns `true` and product not found via `GetByNumericIdAsync` or `GetByShopifyGidAsync`
    - **Property 4: DeleteByNumericIdAsync returns false for non-existent product** — generate random `long` not in DB; assert returns `false` without exception
    - Tag each: `// Feature: database-integration, Property N: <text>`
    - **Validates: Requirements 4.2, 4.3, 4.4, 4.5**

  - [x] 2.8 Create `IWebhookEventRepository` interface and `WebhookEventRepository` implementation
    - Create `ShopifyIntegration/Data/Repositories/IWebhookEventRepository.cs` with methods: `AddAsync`, `GetByTopicAsync`
    - Create `ShopifyIntegration/Data/Repositories/WebhookEventRepository.cs` implementing `IWebhookEventRepository` using `ShopifyDbContext`
    - `AddAsync`: call `_db.WebhookEvents.Add(webhookEvent)` then `SaveChangesAsync`; return the saved entity
    - _Requirements: 5.1, 5.2_

  - [ ]* 2.9 Write property test for `WebhookEventRepository` (Property 5)
    - Add to `ShopifyIntegration.Tests/DatabaseIntegration/WebhookEventRepositoryTests.cs`
    - Use `Microsoft.EntityFrameworkCore.InMemory` provider
    - **Property 5: AddAsync inserts a WebhookEvent and returns it with a generated id** — generate random `WebhookEvent`; assert returned `Id > 0` and all fields match input
    - Tag: `// Feature: database-integration, Property 5: AddAsync inserts WebhookEvent`
    - **Validates: Requirements 5.2**

- [x] 3. Checkpoint — verify data layer compiles and all data-layer tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Update `appsettings.json` and register services in `Program.cs`
  - [x] 4.1 Add `ConnectionStrings` section to `appsettings.json`
    - Add the following to `ShopifyIntegration/appsettings.json`:
      ```json
      "ConnectionStrings": {
        "DefaultConnection": "Host=localhost;Port=5432;Database=ShopifyDB;Username=<user>;Password=<password>"
      }
      ```
    - _Requirements: 1.1_

  - [x] 4.2 Update `Program.cs` — validate connection string, register DbContext and repositories, run startup migrations
    - Read `ConnectionStrings:DefaultConnection`; throw `InvalidOperationException` with a descriptive message if absent or empty (before `builder.Build()`)
    - Register `ShopifyDbContext` with `options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(30), errorCodesToAdd: null))`
    - Register `IProductRepository` → `ProductRepository` as scoped
    - Register `IWebhookEventRepository` → `WebhookEventRepository` as scoped
    - After `var app = builder.Build()`, add a startup migration runner: create a scope, resolve `ShopifyDbContext`, call `await db.Database.MigrateAsync()` inside a try/catch that logs at `Critical` level and calls `Environment.Exit(1)` on failure
    - _Requirements: 1.1, 1.2, 1.3, 8.1, 8.2, 9.1_

- [x] 5. Generate EF Core migration
  - Run `dotnet ef migrations add InitialCreate --project ShopifyIntegration --startup-project ShopifyIntegration` from the solution root to scaffold the initial migration that creates the `products` and `webhook_events` tables
  - Verify the generated migration file creates both tables with the correct columns, types, and the unique index on `products.shopify_gid`
  - _Requirements: 2.1, 2.2, 2.3, 3.1, 3.2_

- [x] 6. Update `ShopifyGraphQLService` to persist products
  - [x] 6.1 Inject `IProductRepository` and add `MapToEntity` helper
    - Add `IProductRepository _productRepository` constructor parameter to `ShopifyGraphQLService`
    - Add private `MapToEntity(ShopifyProduct p)` helper that maps `ShopifyProduct` fields to a `Product` entity, using `ShopifyGidHelper.ParseNumericId` to populate `NumericId`
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 6.2 Persist product on `CreateProductAsync` and `UpdateProductAsync`
    - After a successful response where `ProductCreate?.Product` or `ProductUpdate?.Product` is non-null, call `await _productRepository.UpsertAsync(MapToEntity(p), ct)` before returning
    - Log and re-throw any exception from the repository call
    - _Requirements: 6.1, 6.2, 6.4_

  - [x] 6.3 Remove product on `DeleteProductAsync`
    - After a successful `DeleteProductAsync` response, call `await _productRepository.DeleteByNumericIdAsync(ShopifyGidHelper.ParseNumericId(productId), ct)` before returning
    - Log and re-throw any exception from the repository call
    - _Requirements: 6.3, 6.4_

  - [ ]* 6.4 Write property tests for `ShopifyGraphQLService` persistence (Properties 6–7)
    - Create `ShopifyIntegration.Tests/DatabaseIntegration/ShopifyGraphQLServicePersistenceTests.cs`
    - Use `NSubstitute` to mock `IProductRepository`
    - **Property 6: UpsertAsync called once with matching entity on create/update** — generate random `ShopifyProduct` response data; assert `UpsertAsync` called exactly once with entity whose `ShopifyGid`, `Title`, `Vendor` match the response
    - **Property 7: DeleteByNumericIdAsync called with correct numeric id on delete** — generate random valid Shopify GID strings; assert `DeleteByNumericIdAsync` called exactly once with the numeric id parsed from the GID
    - Tag each: `// Feature: database-integration, Property N: <text>`
    - **Validates: Requirements 6.1, 6.2, 6.3**

- [x] 7. Update `ShopifyWebhookService` to persist webhook events and sync products
  - [x] 7.1 Inject `IProductRepository` and `IWebhookEventRepository`
    - Add `IProductRepository _productRepository` and `IWebhookEventRepository _webhookEventRepository` constructor parameters to `ShopifyWebhookService`
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 7.2 Refactor `ProcessAsync` to async topic handlers with persistence
    - Convert `HandleCreate`, `HandleUpdate`, `HandleDelete`, and `HandleUnknown` to `async Task<WebhookResult>` methods
    - `HandleCreateAsync` / `HandleUpdateAsync`: deserialize payload, call `_productRepository.UpsertAsync` with a mapped `Product` entity, then call `_webhookEventRepository.AddAsync` with a `WebhookEvent` where `Status = "processed"`
    - `HandleDeleteAsync`: deserialize payload, call `_productRepository.DeleteByNumericIdAsync(webhook.Id)`, then call `_webhookEventRepository.AddAsync` with `Status = "processed"`
    - `HandleUnknownAsync`: call `_webhookEventRepository.AddAsync` with `Status = "skipped"`, return `WebhookResult.Success`
    - Update the `topic switch` in `ProcessAsync` to `await` each async handler
    - _Requirements: 7.1, 7.2, 7.3, 7.5_

  - [x] 7.3 Record failed webhook event on exception
    - In the `catch` block of `ProcessAsync`, call `_webhookEventRepository.AddAsync` with a `WebhookEvent` where `Status = "failed"` and `ErrorMessage = ex.Message` (best-effort, swallow any secondary exception)
    - Return `WebhookResult.Failure(ex.Message)`
    - _Requirements: 7.4_

  - [ ]* 7.4 Write property tests for `ShopifyWebhookService` persistence (Properties 8–11)
    - Create `ShopifyIntegration.Tests/DatabaseIntegration/ShopifyWebhookServicePersistenceTests.cs`
    - Use `NSubstitute` to mock `IProductRepository` and `IWebhookEventRepository`
    - **Property 8: UpsertAsync + AddAsync(status="processed") called for products/create and products/update** — generate random `ProductCreatedWebhook` / `ProductUpdatedWebhook` JSON; assert both repository methods called with correct arguments
    - **Property 9: DeleteByNumericIdAsync + AddAsync(status="processed") called for products/delete** — generate random `ProductDeletedWebhook` JSON; assert both repository methods called with correct arguments
    - **Property 10: AddAsync(status="failed", errorMessage=ex.Message) called and Failure returned on exception** — configure mock to throw; assert `AddAsync` called with `Status = "failed"` and `ErrorMessage` matching exception; assert result `IsSuccess == false`
    - **Property 11: AddAsync(status="skipped") called and Success returned for unknown topics** — generate random strings not in `{"products/create","products/update","products/delete"}`; assert `AddAsync` called with `Status = "skipped"`; assert result `IsSuccess == true`
    - Tag each: `// Feature: database-integration, Property N: <text>`
    - **Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5**

- [x] 8. Final checkpoint — ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for a faster MVP
- Properties 1–5 use the EF Core InMemory provider (no real database required)
- Properties 6–11 use NSubstitute mocks (no real database required)
- Property 12 is a pure unit test with no database dependency
- The EF Core migration (task 5) requires a running PostgreSQL instance or can be generated with `--no-build` against the compiled output
- Actual database credentials must be supplied via environment variables or user secrets and must never be committed to source control
- Each property test is tagged with `// Feature: database-integration, Property N: <text>` for traceability
