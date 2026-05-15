# Implementation Plan: CQRS/MediatR Refactor

## Overview

Refactor the ShopifyIntegration ASP.NET Core application from a service-based architecture to CQRS using MediatR. The plan proceeds in layers: dependencies first, then domain/infrastructure relocation, then pipeline infrastructure, then feature handlers, then controller rewrites, then DI wiring, then cleanup, and finally tests. Each step builds on the previous so there is no orphaned code at any stage.

## Tasks

- [x] 1. Add NuGet package dependencies
  - Add `MediatR` (version 12.*) to `ShopifyIntegration.csproj`
  - Add `FluentValidation.AspNetCore` (version 11.*) to `ShopifyIntegration.csproj`
  - Add `FsCheck` and `FsCheck.Xunit` (or `FsCheck.NUnit`) for property-based tests to `ShopifyIntegration.csproj`
  - Verify the project still builds after adding packages
  - _Requirements: 1.1, 1.2_

- [x] 2. Create new folder structure (empty placeholder files are not needed — just establish the directories by placing the first real file in each)
  - Target directories: `Domain/Entities/`, `Domain/Repositories/`, `Infrastructure/Data/Configurations/`, `Infrastructure/Data/Repositories/`, `Infrastructure/Data/Helpers/`, `Infrastructure/Shopify/Validators/`, `Features/Products/Commands/`, `Features/Products/Queries/`, `Features/Webhooks/Commands/`, `Features/Webhooks/Models/`, `Pipeline/`, `Middleware/`, `API/Controllers/`
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

- [x] 3. Relocate domain entities and repository interfaces to `Domain/`
  - [x] 3.1 Move `Data/Entities/Product.cs` → `Domain/Entities/Product.cs`; update namespace to `ShopifyIntegration.Domain.Entities`
    - Update all `using` references across the project
    - _Requirements: 2.3_
  - [x] 3.2 Move `Data/Entities/WebhookEvent.cs` → `Domain/Entities/WebhookEvent.cs`; update namespace to `ShopifyIntegration.Domain.Entities`
    - Update all `using` references across the project
    - _Requirements: 2.3_
  - [x] 3.3 Move `Data/Repositories/IProductRepository.cs` → `Domain/Repositories/IProductRepository.cs`; update namespace to `ShopifyIntegration.Domain.Repositories`
    - Update all `using` references across the project
    - _Requirements: 2.4_
  - [x] 3.4 Move `Data/Repositories/IWebhookEventRepository.cs` → `Domain/Repositories/IWebhookEventRepository.cs`; update namespace to `ShopifyIntegration.Domain.Repositories`
    - Add the new `ExistsProcessedAsync(string topic, long shopifyNumericId, CancellationToken ct)` method to the interface
    - Update all `using` references across the project
    - _Requirements: 2.4, 8.1_

- [x] 4. Relocate infrastructure files to `Infrastructure/`
  - [x] 4.1 Move `Data/ShopifyDbContext.cs` → `Infrastructure/Data/ShopifyDbContext.cs`; update namespace to `ShopifyIntegration.Infrastructure.Data`
    - Update `using ShopifyIntegration.Domain.Entities` references inside the file
    - Update all `using` references across the project (Program.cs, repositories, migrations snapshot)
    - _Requirements: 2.5, 11.1_
  - [x] 4.2 Move `Data/Configurations/ProductConfiguration.cs` and `WebhookEventConfiguration.cs` → `Infrastructure/Data/Configurations/`; update namespaces
    - Update entity `using` references to `ShopifyIntegration.Domain.Entities`
    - _Requirements: 2.5, 11.1_
  - [x] 4.3 Move `Data/Repositories/ProductRepository.cs` → `Infrastructure/Data/Repositories/ProductRepository.cs`; update namespace to `ShopifyIntegration.Infrastructure.Data.Repositories`
    - Update `using` references to `ShopifyIntegration.Domain.Entities` and `ShopifyIntegration.Domain.Repositories`
    - _Requirements: 2.5, 11.1_
  - [x] 4.4 Move `Data/Repositories/WebhookEventRepository.cs` → `Infrastructure/Data/Repositories/WebhookEventRepository.cs`; update namespace to `ShopifyIntegration.Infrastructure.Data.Repositories`
    - Implement the new `ExistsProcessedAsync` method: query `WebhookEvents` where `Topic == topic && ShopifyNumericId == shopifyNumericId && Status == "processed"` and return `AnyAsync`
    - Update `using` references to `ShopifyIntegration.Domain.Entities` and `ShopifyIntegration.Domain.Repositories`
    - _Requirements: 2.5, 8.1, 11.1_
  - [x] 4.5 Move `Data/Helpers/ShopifyGidHelper.cs` → `Infrastructure/Data/Helpers/ShopifyGidHelper.cs`; update namespace to `ShopifyIntegration.Infrastructure.Data.Helpers`
    - Update all `using` references across the project
    - _Requirements: 2.5_
  - [x] 4.6 Move `Services/Shopify/ShopifyGraphQLService.cs` → `Infrastructure/Shopify/ShopifyGraphQLService.cs`; update namespace to `ShopifyIntegration.Infrastructure.Shopify`
    - Update `using` references to new domain/infrastructure namespaces
    - _Requirements: 2.5, 11.2_
  - [x] 4.7 Move `Webhooks/Validators/ShopifyWebhookValidator.cs` → `Infrastructure/Shopify/Validators/ShopifyWebhookValidator.cs`; update namespace to `ShopifyIntegration.Infrastructure.Shopify.Validators`
    - Update all `using` references across the project
    - _Requirements: 2.5, 7.4, 11.4_

- [x] 5. Extract `IShopifyGraphQLService` interface
  - Create `Infrastructure/Shopify/IShopifyGraphQLService.cs` with namespace `ShopifyIntegration.Infrastructure.Shopify`
  - Declare the four methods: `CreateProductAsync`, `GetProductsAsync`, `UpdateProductAsync`, `DeleteProductAsync` matching the signatures in the design document
  - Make `ShopifyGraphQLService` implement `IShopifyGraphQLService`
  - _Requirements: 11.3_

- [x] 6. Create Pipeline behaviours
  - [x] 6.1 Create `Pipeline/ValidationBehaviour.cs` with namespace `ShopifyIntegration.Pipeline`
    - Implement `IPipelineBehavior<TRequest, TResponse>` exactly as specified in the design document
    - Collect all `IValidator<TRequest>` failures and throw `ValidationException` when any exist
    - _Requirements: 1.4, 9.5_
  - [x] 6.2 Create `Pipeline/LoggingBehaviour.cs` with namespace `ShopifyIntegration.Pipeline`
    - Implement `IPipelineBehavior<TRequest, TResponse>` exactly as specified in the design document
    - Log request name + correlation ID at start; log elapsed ms on success; log exception at Error level on failure before re-throwing
    - _Requirements: 1.5, 10.1, 10.2, 10.3_

- [x] 7. Create `GlobalExceptionMiddleware`
  - Create `Middleware/GlobalExceptionMiddleware.cs` with namespace `ShopifyIntegration.Middleware`
  - Catch `ValidationException` → HTTP 400 with `{ "errors": [...] }` JSON body
  - Catch all other exceptions → log at Error level → HTTP 500 with `{ "error": "An unexpected error occurred." }` JSON body
  - _Requirements: 10.4, 10.5, 9.6_

- [x] 8. Create Product Commands, Validators, and Handlers
  - [x] 8.1 Create `Features/Products/Commands/CreateProductCommand.cs`
    - Define `sealed record CreateProductCommand(string Title, string DescriptionHtml, string Vendor) : IRequest<CreateProductResponse?>`
    - _Requirements: 4.1_
  - [x] 8.2 Create `Features/Products/Commands/CreateProductCommandValidator.cs`
    - `RuleFor(x => x.Title).NotEmpty()` and `RuleFor(x => x.Vendor).NotEmpty()`
    - _Requirements: 9.1_
  - [x] 8.3 Create `Features/Products/Commands/CreateProductCommandHandler.cs`
    - Inject `IShopifyGraphQLService`; call `CreateProductAsync` with a `CreateProductGraphQLDto` built from the command; return the response
    - _Requirements: 4.2_
  - [x] 8.4 Create `Features/Products/Commands/UpdateProductCommand.cs`
    - Define `sealed record UpdateProductCommand(string ShopifyGid, string Title, string DescriptionHtml) : IRequest<UpdateProductResponse?>`
    - _Requirements: 4.3_
  - [x] 8.5 Create `Features/Products/Commands/UpdateProductCommandValidator.cs`
    - `RuleFor(x => x.ShopifyGid).NotEmpty()` and `RuleFor(x => x.Title).NotEmpty()`
    - _Requirements: 9.2_
  - [x] 8.6 Create `Features/Products/Commands/UpdateProductCommandHandler.cs`
    - Inject `IShopifyGraphQLService`; call `UpdateProductAsync` with an `UpdateProductGraphQLDto` built from the command; return the response
    - _Requirements: 4.4_
  - [x] 8.7 Create `Features/Products/Commands/DeleteProductCommand.cs`
    - Define `sealed record DeleteProductCommand(string ShopifyGid) : IRequest<DeleteProductResponse?>`
    - _Requirements: 4.5_
  - [x] 8.8 Create `Features/Products/Commands/DeleteProductCommandValidator.cs`
    - `RuleFor(x => x.ShopifyGid).NotEmpty()`
    - _Requirements: 9.3_
  - [x] 8.9 Create `Features/Products/Commands/DeleteProductCommandHandler.cs`
    - Inject `IShopifyGraphQLService`; call `DeleteProductAsync(command.ShopifyGid)`; return the response
    - _Requirements: 4.6_

- [x] 9. Create Product Queries and Handlers
  - [x] 9.1 Create `Features/Products/Queries/GetProductsQuery.cs`
    - Define `sealed record GetProductsQuery() : IRequest<GetProductsResponse?>`
    - _Requirements: 5.1_
  - [x] 9.2 Create `Features/Products/Queries/GetProductsQueryHandler.cs`
    - Inject `IShopifyGraphQLService`; call `GetProductsAsync(ct)`; return the response
    - _Requirements: 5.2, 5.6_
  - [x] 9.3 Create `Features/Products/Queries/GetProductByIdQuery.cs`
    - Define `sealed record GetProductByIdQuery(string ShopifyGid) : IRequest<Product?>`
    - _Requirements: 5.3_
  - [x] 9.4 Create `Features/Products/Queries/GetProductByIdQueryHandler.cs`
    - Inject `IProductRepository`; call `GetByShopifyGidAsync(query.ShopifyGid, ct)`; return the result (null if not found)
    - _Requirements: 5.4, 5.5, 5.6_

- [x] 10. Move webhook payload models to `Features/Webhooks/Models/`
  - Move `Webhooks/Models/Products/ProductCreatedWebhook.cs` → `Features/Webhooks/Models/ProductCreatedWebhook.cs`; update namespace to `ShopifyIntegration.Features.Webhooks.Models`
  - Move `Webhooks/Models/Products/ProductUpdatedWebhook.cs` → `Features/Webhooks/Models/ProductUpdatedWebhook.cs`; update namespace
  - Move `Webhooks/Models/Products/ProductDeletedWebhook.cs` → `Features/Webhooks/Models/ProductDeletedWebhook.cs`; update namespace
  - Update all `using` references across the project
  - _Requirements: 2.2_

- [x] 11. Create Webhook Commands, Validators, and Handlers
  - [x] 11.1 Create `Features/Webhooks/Commands/ProcessShopifyWebhookCommand.cs`
    - Define `sealed record ProcessShopifyWebhookCommand(byte[] RawBody, string Topic) : IRequest<Unit>`
    - _Requirements: 6.1_
  - [x] 11.2 Create `Features/Webhooks/Commands/ProcessShopifyWebhookCommandValidator.cs`
    - `RuleFor(x => x.Topic).NotEmpty()` and `RuleFor(x => x.RawBody).NotEmpty()`
    - _Requirements: 9.4_
  - [x] 11.3 Create `Features/Webhooks/Commands/HandleProductCreatedCommand.cs`
    - Define `sealed record HandleProductCreatedCommand(long NumericId, string Title, string Vendor, string Status, DateTimeOffset UpdatedAt) : IRequest<Unit>`
    - _Requirements: 6.3_
  - [x] 11.4 Create `Features/Webhooks/Commands/HandleProductUpdatedCommand.cs`
    - Define `sealed record HandleProductUpdatedCommand(long NumericId, string Title, string Vendor, string Status, DateTimeOffset UpdatedAt) : IRequest<Unit>`
    - _Requirements: 6.5_
  - [x] 11.5 Create `Features/Webhooks/Commands/HandleProductDeletedCommand.cs`
    - Define `sealed record HandleProductDeletedCommand(long NumericId) : IRequest<Unit>`
    - _Requirements: 6.7_
  - [x] 11.6 Create `Features/Webhooks/Commands/HandleProductCreatedCommandHandler.cs`
    - Inject `IProductRepository` and `IWebhookEventRepository`
    - Call `ExistsProcessedAsync("products/create", command.NumericId, ct)`; if true, return `Unit.Value` (idempotency skip)
    - Otherwise: build `Product` entity using `ShopifyGidHelper.BuildProductGid`, call `UpsertAsync`, then `AddAsync` a `WebhookEvent` with `Status = "processed"`
    - On exception: call `AddAsync` a `WebhookEvent` with `Status = "failed"` and `ErrorMessage = ex.Message`, then re-throw
    - _Requirements: 6.4, 8.2, 8.3, 6.10_
  - [x] 11.7 Create `Features/Webhooks/Commands/HandleProductUpdatedCommandHandler.cs`
    - Same idempotency + upsert + event recording pattern as 11.6, using topic `"products/update"`
    - _Requirements: 6.6, 8.2, 8.3, 6.10_
  - [x] 11.8 Create `Features/Webhooks/Commands/HandleProductDeletedCommandHandler.cs`
    - Inject `IProductRepository` and `IWebhookEventRepository`
    - Call `ExistsProcessedAsync("products/delete", command.NumericId, ct)`; if true, return `Unit.Value`
    - Otherwise: call `DeleteByNumericIdAsync`, then `AddAsync` a `WebhookEvent` with `Status = "processed"`
    - On exception: call `AddAsync` a `WebhookEvent` with `Status = "failed"` and `ErrorMessage = ex.Message`, then re-throw
    - _Requirements: 6.8, 8.2, 8.3, 6.10_
  - [x] 11.9 Create `Features/Webhooks/Commands/ProcessShopifyWebhookCommandHandler.cs`
    - Inject `IMediator` and `IWebhookEventRepository`
    - Deserialise `RawBody` to UTF-8 string; switch on `Topic`:
      - `"products/create"` → deserialise to `ProductCreatedWebhook`, dispatch `HandleProductCreatedCommand`
      - `"products/update"` → deserialise to `ProductUpdatedWebhook`, dispatch `HandleProductUpdatedCommand`
      - `"products/delete"` → deserialise to `ProductDeletedWebhook`, dispatch `HandleProductDeletedCommand`
      - default → `AddAsync` a `WebhookEvent` with `Status = "skipped"`, return `Unit.Value`
    - _Requirements: 6.2, 6.9_

- [x] 12. Checkpoint — verify build
  - Ensure all tests pass, ask the user if questions arise.

- [x] 13. Refactor `ProductsController` to thin MediatR dispatcher
  - Create `API/Controllers/ProductsController.cs` with namespace `ShopifyIntegration.API.Controllers`
  - Inject only `IMediator`; implement the five endpoints exactly as specified in the design document (`CreateProduct`, `GetProducts`, `GetProductById`, `UpdateProduct`, `DeleteProduct`)
  - `GetProductById` returns `NotFound()` when the handler returns null
  - Delete the old `Controllers/ProductController.cs`
  - _Requirements: 3.1, 3.2, 3.3_

- [x] 14. Refactor `WebhooksController` to thin MediatR dispatcher
  - Create `API/Controllers/WebhooksController.cs` with namespace `ShopifyIntegration.API.Controllers`
  - Inject `IMediator` and `IShopifyWebhookValidator`; implement `ReceiveShopifyWebhook` exactly as specified in the design document
  - HMAC validation runs before `IMediator.Send`; return HTTP 401 on failure
  - Delete the old `Controllers/WebhooksController.cs`
  - _Requirements: 3.4, 3.5, 3.6, 7.1, 7.4_

- [x] 15. Update `Program.cs` DI registrations
  - Add `builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly))`
  - Add `LoggingBehaviour` registration as open-generic `IPipelineBehavior<,>` (registered first so it wraps validation)
  - Add `ValidationBehaviour` registration as open-generic `IPipelineBehavior<,>`
  - Add `builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly)`
  - Add `builder.Services.AddScoped<IShopifyGraphQLService, ShopifyGraphQLService>()`
  - Add `app.UseMiddleware<GlobalExceptionMiddleware>()` before `app.UseAuthorization()`
  - Remove `builder.Services.AddScoped<ShopifyGraphQLService>()` (replaced by interface registration)
  - Remove `builder.Services.AddScoped<IShopifyWebhookService, ShopifyWebhookService>()` (replaced by CQRS handlers)
  - Update all `using` directives to reference new namespaces (`Infrastructure.Data`, `Infrastructure.Shopify`, etc.)
  - _Requirements: 1.3, 1.4, 1.5, 1.6, 12.1, 12.2, 12.3, 12.4, 12.5, 12.6, 11.6_

- [x] 16. Remove legacy files that are no longer referenced
  - Delete `Services/Shopify/ShopifyService.cs` (legacy REST service, no longer registered or referenced)
  - Delete `Services/Interfaces/IShopifyService.cs`
  - Delete `Services/Shopify/ShopifyGraphQLService.cs` (moved to `Infrastructure/Shopify/`)
  - Delete `Webhooks/Services/ShopifyWebhookService.cs` (replaced by CQRS handlers)
  - Delete `DTOs/CreateProductDto.cs`, `DTOs/CreateProductGraphQLDto.cs`, `DTOs/UpdateProductGraphQLDto.cs` if they have been superseded by command records (or move them to `Infrastructure/Shopify/` if still needed by `ShopifyGraphQLService`)
  - Delete old `Controllers/` directory once `API/Controllers/` replacements are confirmed
  - Delete old `Data/` directory once all files have been relocated to `Infrastructure/Data/` and `Domain/`
  - Delete old `Webhooks/` directory once models and validators have been relocated
  - _Requirements: 11.6_

- [x] 17. Checkpoint — full build and smoke test
  - Ensure all tests pass, ask the user if questions arise.

- [x] 18. Write property-based tests using FsCheck
  - [x] 18.1 Write property test for Property 1: ValidationBehaviour rejects invalid commands
    - // Feature: cqrs-mediatr-refactor, Property 1: ValidationBehaviour rejects invalid commands before the handler runs
    - Generate random `CreateProductCommand` instances with at least one empty required field using FsCheck `Arb`
    - Assert `ValidationException` is thrown and the mock handler delegate is never invoked
    - **Property 1: ValidationBehaviour rejects invalid commands before the handler runs**
    - **Validates: Requirements 1.4, 9.5**
  - [x] 18.2 Write property test for Property 2: HMAC rejection is universal
    - // Feature: cqrs-mediatr-refactor, Property 2: HMAC rejection is universal
    - Generate random byte arrays and random HMAC header strings (excluding the correct digest) using FsCheck
    - Assert `ShopifyWebhookValidator.Validate` returns `IsValid = false` for all such inputs
    - Assert a mock `IMediator` receives no `Send` calls when the controller receives an invalid HMAC
    - **Property 2: HMAC rejection is universal**
    - **Validates: Requirements 3.6, 7.4**
  - [x] 18.3 Write property test for Property 3: Query handlers perform no writes
    - // Feature: cqrs-mediatr-refactor, Property 3: Query handlers perform no writes
    - Generate random `GetProductsQuery` and `GetProductByIdQuery` inputs using FsCheck
    - Assert no write methods (`UpsertAsync`, `DeleteByNumericIdAsync`, `AddAsync`) are called on mocked repositories
    - **Property 3: Query handlers perform no writes**
    - **Validates: Requirements 5.6**
  - [x] 18.4 Write property test for Property 4: ProcessShopifyWebhookCommand dispatches correct sub-command
    - // Feature: cqrs-mediatr-refactor, Property 4: ProcessShopifyWebhookCommand dispatches the correct topic-specific command
    - Generate random valid payloads for each of the three recognised topics using FsCheck
    - Assert the correct `Handle*Command` type is dispatched via a mocked `IMediator` and no other command type is dispatched
    - **Property 4: ProcessShopifyWebhookCommand dispatches the correct topic-specific command**
    - **Validates: Requirements 6.2**
  - [x] 18.5 Write property test for Property 5: Webhook handlers are idempotent
    - // Feature: cqrs-mediatr-refactor, Property 5: Webhook handlers are idempotent
    - Generate random `HandleProductCreatedCommand`, `HandleProductUpdatedCommand`, and `HandleProductDeletedCommand` inputs using FsCheck
    - Configure mocked `IWebhookEventRepository.ExistsProcessedAsync` to return `true`
    - Assert `IProductRepository.UpsertAsync` and `DeleteByNumericIdAsync` are never called
    - **Property 5: Webhook handlers are idempotent**
    - **Validates: Requirements 8.2, 8.3**
  - [x] 18.6 Write property test for Property 6: Failed webhook handlers record "failed" and re-throw
    - // Feature: cqrs-mediatr-refactor, Property 6: Failed webhook handlers record a "failed" event and re-throw
    - Generate random exceptions from the mocked `IProductRepository` using FsCheck
    - Assert `IWebhookEventRepository.AddAsync` is called with `Status = "failed"` and the exception message
    - Assert the original exception is re-thrown
    - **Property 6: Failed webhook handlers record a "failed" event and re-throw**
    - **Validates: Requirements 6.10**
  - [x] 18.7 Write property test for Property 7: ValidationException maps to HTTP 400
    - // Feature: cqrs-mediatr-refactor, Property 7: ValidationException maps to HTTP 400
    - Generate random `ValidationException` instances with varying failure messages using FsCheck
    - Assert `GlobalExceptionMiddleware` returns HTTP 400 and the response body contains all failure messages as JSON
    - **Property 7: ValidationException maps to HTTP 400**
    - **Validates: Requirements 9.6, 10.5**

- [x] 19. Write unit tests for handlers and validators
  - [x] 19.1 Write unit tests for `CreateProductCommandHandler`
    - Verify `IShopifyGraphQLService.CreateProductAsync` is called with the correct `CreateProductGraphQLDto` arguments
    - _Requirements: 4.2_
  - [x] 19.2 Write unit tests for `GetProductByIdQueryHandler`
    - Verify `IProductRepository.GetByShopifyGidAsync` is called with the correct GID
    - Verify `null` is returned when the repository returns `null`
    - _Requirements: 5.4, 5.5_
  - [x] 19.3 Write unit tests for `ProcessShopifyWebhookCommandHandler`
    - Verify the correct sub-command type is dispatched for each of the three recognised topics
    - Verify a `WebhookEvent` with `Status = "skipped"` is recorded for an unrecognised topic
    - _Requirements: 6.2, 6.9_
  - [x] 19.4 Write unit tests for `HandleProductCreatedCommandHandler`
    - Verify idempotency: when `ExistsProcessedAsync` returns `true`, `UpsertAsync` is not called
    - Verify normal path: `UpsertAsync` is called and a `WebhookEvent` with `Status = "processed"` is recorded
    - _Requirements: 6.4, 8.2, 8.3_
  - [x] 19.5 Write unit tests for FluentValidation validators
    - `CreateProductCommandValidator`: empty title → fails; empty vendor → fails; both populated → passes
    - `UpdateProductCommandValidator`: empty GID → fails; empty title → fails; both populated → passes
    - `DeleteProductCommandValidator`: empty GID → fails; non-empty GID → passes
    - `ProcessShopifyWebhookCommandValidator`: empty topic → fails; empty body → fails; both populated → passes
    - _Requirements: 9.1, 9.2, 9.3, 9.4_
  - [x] 19.6 Write unit tests for `GlobalExceptionMiddleware`
    - `ValidationException` → HTTP 400 with errors JSON
    - Generic `Exception` → HTTP 500 with error JSON
    - _Requirements: 10.4, 10.5_

- [x] 20. Final checkpoint — full build and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- The `ExistsProcessedAsync` method added in task 3.4 is implemented in task 4.4 — both must be done before webhook handlers in task 11
- DTOs (`CreateProductGraphQLDto`, `UpdateProductGraphQLDto`) are still needed by `ShopifyGraphQLService`; keep them or move them to `Infrastructure/Shopify/` rather than deleting them
- No new EF Core migrations are required — only namespace changes to existing entity and configuration files
- The `Migrations/` folder and `ShopifyDbContextModelSnapshot.cs` reference the old `ShopifyDbContext` namespace; update those `using` statements as part of task 4.1
- Property tests require a minimum of 100 iterations each (FsCheck default is 100)
