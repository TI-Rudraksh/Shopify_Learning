# Requirements Document

## Introduction

This document captures the requirements for refactoring the existing ASP.NET Core Shopify-integrated application into a CQRS (Command Query Responsibility Segregation) architecture using MediatR. The refactor separates all read operations into Queries and all write/update/delete operations into Commands, each with dedicated Handlers. Shopify webhook processing is moved into CQRS command handlers. Controllers become thin dispatchers that communicate exclusively through MediatR. The project is reorganised into a feature-based folder structure (Products, Webhooks, etc.) with clean separation between the API, Application, Domain, and Infrastructure layers. FluentValidation is introduced for request validation.

The existing functionality — product CRUD via Shopify GraphQL, webhook ingestion with HMAC validation, idempotency, and EF Core persistence — is preserved throughout the refactor.

---

## Glossary

- **Application**: The layer containing Commands, Queries, Handlers, Validators, and DTOs. Contains no infrastructure or framework dependencies beyond MediatR and FluentValidation abstractions.
- **Command**: A MediatR `IRequest<TResponse>` that represents a write, update, or delete intent. Commands are handled by a single `IRequestHandler`.
- **CQRS**: Command Query Responsibility Segregation — an architectural pattern that separates read models (Queries) from write models (Commands).
- **Domain**: The layer containing entity definitions and repository interfaces. Has no dependencies on infrastructure or application layers.
- **FluentValidation**: A .NET library for building strongly-typed validation rules, integrated with MediatR via a pipeline behaviour.
- **Handler**: A class implementing `IRequestHandler<TRequest, TResponse>` that contains the business logic for a single Command or Query.
- **Infrastructure**: The layer containing EF Core `DbContext`, repository implementations, Shopify API clients, and webhook validators.
- **MediatR**: A .NET in-process messaging library that decouples request senders from handlers via an `IMediator` interface.
- **Pipeline Behaviour**: A MediatR `IPipelineBehavior<TRequest, TResponse>` that wraps handler execution for cross-cutting concerns such as validation and logging.
- **Query**: A MediatR `IRequest<TResponse>` that represents a read-only data retrieval intent. Queries must not produce side effects.
- **Shopify_GraphQL_Service**: The infrastructure service that communicates with the Shopify Admin GraphQL API.
- **Shopify_REST_Service**: The legacy infrastructure service that communicates with the Shopify Admin REST API (retained for backward compatibility during transition).
- **Webhook_Command**: A Command that encapsulates a validated, raw Shopify webhook payload and its topic for processing by a dedicated handler.
- **Webhook_Validator**: The component that performs HMAC-SHA256 signature verification on incoming Shopify webhook requests.

---

## Requirements

### Requirement 1: MediatR Integration and Project Dependencies

**User Story:** As a developer, I want MediatR and FluentValidation added to the project, so that the CQRS infrastructure is available for all features.

#### Acceptance Criteria

1. THE Application SHALL reference the `MediatR` NuGet package (version compatible with net10.0).
2. THE Application SHALL reference the `FluentValidation.AspNetCore` NuGet package (version compatible with net10.0).
3. WHEN the application starts, THE Application SHALL register MediatR with all handlers discovered from the application assembly.
4. WHEN the application starts, THE Application SHALL register a `ValidationBehaviour` pipeline behaviour that runs FluentValidation validators before every Command and Query handler.
5. WHEN the application starts, THE Application SHALL register a `LoggingBehaviour` pipeline behaviour that logs the request name, execution time, and outcome for every Command and Query.
6. THE Application SHALL register all FluentValidation validators discovered from the application assembly.

---

### Requirement 2: Feature-Based Folder Structure

**User Story:** As a developer, I want the project organised by feature (Products, Webhooks, etc.), so that all related Commands, Queries, Handlers, DTOs, and Validators for a feature are co-located and easy to navigate.

#### Acceptance Criteria

1. THE Application SHALL organise source files under a top-level `Features/` directory, with one sub-directory per feature (e.g., `Features/Products/`, `Features/Webhooks/`).
2. THE Application SHALL place Commands, Queries, Handlers, DTOs, and Validators for each feature inside that feature's sub-directory.
3. THE Application SHALL place domain entity definitions under a `Domain/` directory.
4. THE Application SHALL place repository interfaces under the `Domain/` directory.
5. THE Application SHALL place EF Core `DbContext`, repository implementations, and Shopify API clients under an `Infrastructure/` directory.
6. THE Application SHALL place ASP.NET Core controllers under an `API/Controllers/` directory.

---

### Requirement 3: Thin Controllers via MediatR Dispatch

**User Story:** As a developer, I want controllers to contain no business logic, so that they remain thin and all logic is testable through MediatR handlers.

#### Acceptance Criteria

1. THE ProductsController SHALL inject `IMediator` as its only dependency.
2. WHEN a request is received by the ProductsController, THE ProductsController SHALL construct the appropriate Command or Query and dispatch it via `IMediator.Send`.
3. THE ProductsController SHALL map the handler result to an HTTP response (200, 201, 400, 404, 500) without containing any business logic.
4. THE WebhooksController SHALL inject `IMediator` as its only dependency.
5. WHEN a webhook request is received by the WebhooksController, THE WebhooksController SHALL read the raw body and headers, validate the HMAC signature, construct a `ProcessShopifyWebhookCommand`, and dispatch it via `IMediator.Send`.
6. IF HMAC validation fails in the WebhooksController, THEN THE WebhooksController SHALL return HTTP 401 without dispatching a command.

---

### Requirement 4: Product Commands

**User Story:** As a developer, I want all product write operations expressed as Commands with dedicated Handlers, so that each operation has a single, testable unit of business logic.

#### Acceptance Criteria

1. THE Application SHALL define a `CreateProductCommand` containing the product title, description HTML, and vendor.
2. WHEN a `CreateProductCommand` is dispatched, THE CreateProductCommandHandler SHALL call the Shopify GraphQL API to create the product and persist the result to the database via the product repository.
3. THE Application SHALL define an `UpdateProductCommand` containing the Shopify GID, updated title, and updated description HTML.
4. WHEN an `UpdateProductCommand` is dispatched, THE UpdateProductCommandHandler SHALL call the Shopify GraphQL API to update the product and persist the result to the database via the product repository.
5. THE Application SHALL define a `DeleteProductCommand` containing the Shopify GID of the product to delete.
6. WHEN a `DeleteProductCommand` is dispatched, THE DeleteProductCommandHandler SHALL call the Shopify GraphQL API to delete the product and remove the corresponding record from the database via the product repository.
7. IF the Shopify GraphQL API returns an error for any product command, THEN THE corresponding CommandHandler SHALL throw a domain exception that is caught by the pipeline behaviour and returned as a structured error response.

---

### Requirement 5: Product Queries

**User Story:** As a developer, I want all product read operations expressed as Queries with dedicated Handlers, so that reads are clearly separated from writes and have no side effects.

#### Acceptance Criteria

1. THE Application SHALL define a `GetProductsQuery` with no input parameters.
2. WHEN a `GetProductsQuery` is dispatched, THE GetProductsQueryHandler SHALL retrieve the product list from the Shopify GraphQL API and return it as a typed response DTO.
3. THE Application SHALL define a `GetProductByIdQuery` containing the Shopify GID of the product to retrieve.
4. WHEN a `GetProductByIdQuery` is dispatched, THE GetProductByIdQueryHandler SHALL retrieve the product from the local database via the product repository and return it as a typed response DTO.
5. IF a product is not found for a `GetProductByIdQuery`, THEN THE GetProductByIdQueryHandler SHALL return a null or empty result that the controller maps to HTTP 404.
6. WHILE handling any Query, THE QueryHandler SHALL perform no write operations against the database or the Shopify API.

---

### Requirement 6: Webhook Command Handlers

**User Story:** As a developer, I want Shopify webhook processing moved into CQRS command handlers, so that webhook logic is testable, auditable, and consistent with the rest of the application.

#### Acceptance Criteria

1. THE Application SHALL define a `ProcessShopifyWebhookCommand` containing the raw body bytes and the webhook topic string.
2. WHEN a `ProcessShopifyWebhookCommand` is dispatched, THE ProcessShopifyWebhookCommandHandler SHALL deserialise the payload according to the topic and dispatch a topic-specific command (e.g., `HandleProductCreatedCommand`, `HandleProductUpdatedCommand`, `HandleProductDeletedCommand`).
3. THE Application SHALL define a `HandleProductCreatedCommand` containing the deserialised product data from the webhook payload.
4. WHEN a `HandleProductCreatedCommand` is dispatched, THE HandleProductCreatedCommandHandler SHALL upsert the product in the database and record a `WebhookEvent` with status `"processed"`.
5. THE Application SHALL define a `HandleProductUpdatedCommand` containing the deserialised product data from the webhook payload.
6. WHEN a `HandleProductUpdatedCommand` is dispatched, THE HandleProductUpdatedCommandHandler SHALL upsert the product in the database and record a `WebhookEvent` with status `"processed"`.
7. THE Application SHALL define a `HandleProductDeletedCommand` containing the numeric Shopify product ID from the webhook payload.
8. WHEN a `HandleProductDeletedCommand` is dispatched, THE HandleProductDeletedCommandHandler SHALL delete the product from the database and record a `WebhookEvent` with status `"processed"`.
9. IF the webhook topic is not recognised, THEN THE ProcessShopifyWebhookCommandHandler SHALL record a `WebhookEvent` with status `"skipped"` and return a success result.
10. IF an exception occurs during webhook command handling, THEN THE corresponding Handler SHALL record a `WebhookEvent` with status `"failed"` and the exception message, then re-throw so the pipeline behaviour can log the failure.

---

### Requirement 7: HMAC Validation Preservation

**User Story:** As a developer, I want HMAC validation to remain intact after the refactor, so that only authentic Shopify webhook requests are processed.

#### Acceptance Criteria

1. THE WebhooksController SHALL invoke the `Webhook_Validator` before dispatching any webhook command.
2. WHEN a webhook request is received, THE Webhook_Validator SHALL compute an HMAC-SHA256 digest of the raw request body using the configured `WebhookSecret`.
3. THE Webhook_Validator SHALL compare the computed digest to the `X-Shopify-Hmac-SHA256` header value using a constant-time comparison.
4. IF the HMAC comparison fails, THEN THE Webhook_Validator SHALL return an invalid result and THE WebhooksController SHALL return HTTP 401 without processing the payload.
5. IF the `WebhookSecret` configuration value is empty or missing, THEN THE Webhook_Validator SHALL return an invalid result.

---

### Requirement 8: Idempotency for Webhook Events

**User Story:** As a developer, I want webhook event records to support idempotency checks, so that duplicate webhook deliveries do not corrupt the database state.

#### Acceptance Criteria

1. THE Application SHALL define an `IWebhookEventRepository` method to check whether a `WebhookEvent` with a given topic and `ShopifyNumericId` has already been processed with status `"processed"`.
2. WHEN a `HandleProductCreatedCommand`, `HandleProductUpdatedCommand`, or `HandleProductDeletedCommand` is dispatched, THE corresponding Handler SHALL check for an existing processed event before performing any write operation.
3. IF a matching processed `WebhookEvent` already exists, THEN THE Handler SHALL skip the write operation and return a success result without recording a duplicate event.
4. THE `WebhookEvent` entity SHALL store the topic, numeric Shopify ID, raw payload, processed timestamp, status, and optional error message.

---

### Requirement 9: FluentValidation for Commands and Queries

**User Story:** As a developer, I want all Commands and Queries validated with FluentValidation before reaching their handlers, so that invalid requests are rejected early with descriptive error messages.

#### Acceptance Criteria

1. THE Application SHALL define a `CreateProductCommandValidator` that requires a non-empty title and vendor.
2. THE Application SHALL define an `UpdateProductCommandValidator` that requires a non-empty Shopify GID and title.
3. THE Application SHALL define a `DeleteProductCommandValidator` that requires a non-empty Shopify GID.
4. THE Application SHALL define a `ProcessShopifyWebhookCommandValidator` that requires a non-empty topic and a non-empty raw body.
5. WHEN a Command or Query fails validation, THE ValidationBehaviour SHALL throw a `ValidationException` containing all validation failure messages.
6. IF a `ValidationException` is thrown, THEN THE Application SHALL return HTTP 400 with the validation failure details without invoking the handler.

---

### Requirement 10: Logging and Error Handling Pipeline

**User Story:** As a developer, I want structured logging and consistent error handling applied to every Command and Query, so that failures are observable and the API returns predictable error responses.

#### Acceptance Criteria

1. THE LoggingBehaviour SHALL log the request type name and a unique correlation identifier at the start of every handler execution.
2. WHEN a handler completes successfully, THE LoggingBehaviour SHALL log the elapsed time in milliseconds.
3. IF a handler throws an unhandled exception, THEN THE LoggingBehaviour SHALL log the exception details at the Error level before re-throwing.
4. THE Application SHALL include a global exception handler middleware that maps unhandled exceptions to structured HTTP error responses (HTTP 500 with a JSON error body).
5. IF a `ValidationException` is thrown, THEN THE global exception handler SHALL return HTTP 400 with the validation failure messages serialised as JSON.

---

### Requirement 11: Infrastructure Layer Preservation

**User Story:** As a developer, I want the existing EF Core data layer, Shopify API clients, and database migrations preserved during the refactor, so that no data is lost and the database schema remains unchanged.

#### Acceptance Criteria

1. THE Infrastructure layer SHALL retain the `ShopifyDbContext`, `ProductRepository`, and `WebhookEventRepository` implementations without schema changes.
2. THE Infrastructure layer SHALL retain the `ShopifyGraphQLService` as the primary Shopify API client, injected into command and query handlers via its interface.
3. THE Infrastructure layer SHALL expose `IShopifyGraphQLService` as an interface so handlers depend on the abstraction, not the concrete class.
4. THE Infrastructure layer SHALL retain the `ShopifyWebhookValidator` implementation unchanged.
5. THE Application SHALL not require new EF Core migrations as a result of this refactor.
6. WHERE the legacy `ShopifyService` (REST-based) is no longer referenced by any handler or controller, THE Application SHALL remove it from the DI registration.

---

### Requirement 12: Dependency Injection Registration

**User Story:** As a developer, I want all new CQRS components registered in the DI container at startup, so that the application resolves handlers, validators, and behaviours correctly.

#### Acceptance Criteria

1. WHEN the application starts, THE Application SHALL register MediatR and scan the application assembly for all `IRequestHandler` implementations.
2. WHEN the application starts, THE Application SHALL register the `ValidationBehaviour` as an open-generic `IPipelineBehavior<,>`.
3. WHEN the application starts, THE Application SHALL register the `LoggingBehaviour` as an open-generic `IPipelineBehavior<,>`.
4. WHEN the application starts, THE Application SHALL register all FluentValidation `AbstractValidator<T>` implementations discovered from the application assembly.
5. THE Application SHALL register `IShopifyGraphQLService` mapped to `ShopifyGraphQLService` with a scoped lifetime.
6. THE Application SHALL retain the existing registrations for `IProductRepository`, `IWebhookEventRepository`, `ShopifyDbContext`, and `IShopifyWebhookValidator`.
