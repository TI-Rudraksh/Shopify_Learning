# Implementation Plan: Partial Order Fulfillment

## Overview

Implement the `POST /api/orders/{orderId}/fulfill-items` endpoint following the existing CQRS pattern (MediatR + FluentValidation). The work spans six layers: domain entity change, EF Core schema migration, new GraphQL response models and query constant, a new `FulfillLineItemsAsync` method on `ShopifyFulfillmentService`, a new CQRS command/validator/handler, and a new controller action. A dedicated test project is created to host FsCheck property-based tests and xUnit unit tests.

---

## Tasks

- [x] 1. Extend the `Fulfillment` entity and EF Core configuration
  - [x] 1.1 Add `FulfilledLineItemGids` property to `Fulfillment` entity
    - Open `Domain/Entities/Fulfillment.cs` and add `public string? FulfilledLineItemGids { get; set; }` with an inline comment: `// JSON array of OrderLineItem ShopifyGids`
    - _Requirements: 5.1, 5.2, 6.1, 6.2_

  - [x] 1.2 Map the new column in `FulfillmentConfiguration`
    - Open `Infrastructure/Data/Configurations/FulfillmentConfiguration.cs` and add `builder.Property(f => f.FulfilledLineItemGids).HasColumnName("fulfilled_line_item_gids");` after the existing `FulfillmentOrderGid` mapping
    - _Requirements: 5.1_

  - [x] 1.3 Generate and apply the EF Core migration
    - Run `dotnet ef migrations add AddFulfilledLineItemGids --project Shopify_Learning` from the solution root to create a migration that adds the nullable `fulfilled_line_item_gids` text column to the `fulfillments` table
    - Verify the generated `Up` method adds the column and the `Down` method drops it
    - _Requirements: 5.1_

- [x] 2. Add GraphQL response models and query constant
  - [x] 2.1 Create `GetFulfillmentOrdersWithLineItemsResponse` response classes
    - Create `GraphQL/Responses/Fulfillment/GetFulfillmentOrdersWithLineItemsResponse.cs` with all classes defined in the design: `GetFulfillmentOrdersWithLineItemsResponse`, `ShopifyOrderWithLineItemsNode`, `ShopifyFulfillmentOrderWithLineItemsConnection`, `ShopifyFulfillmentOrderWithLineItemsEdge`, `ShopifyFulfillmentOrderWithLineItemsNode`, `ShopifyFulfillmentOrderLineItemConnection`, `ShopifyFulfillmentOrderLineItemEdge`, `ShopifyFulfillmentOrderLineItemNode`, and `ShopifyLineItemReference`
    - _Requirements: 4.2, 4.3_

  - [x] 2.2 Add `GetFulfillmentOrdersWithLineItems` query constant to `FulfillmentQueries`
    - Open `GraphQL/Queries/FulfillmentQueries.cs` and add the `GetFulfillmentOrdersWithLineItems` constant with the GraphQL query that retrieves `fulfillmentOrders` including `lineItems` edges with `id` and `lineItem { id }` as specified in the design
    - _Requirements: 4.2_

- [x] 3. Add `FulfillLineItemsAsync` to `IShopifyFulfillmentService` and implement it
  - [x] 3.1 Declare `FulfillLineItemsAsync` on the interface
    - Open `Infrastructure/Shopify/IShopifyFulfillmentService.cs` and add the method signature: `Task<FulfillmentCreatePayload> FulfillLineItemsAsync(string orderGid, List<string> lineItemGids, string? trackingNumber = null, string? trackingCompany = null, bool notifyCustomer = true, CancellationToken ct = default);`
    - _Requirements: 4.1_

  - [x] 3.2 Implement `FulfillLineItemsAsync` in `ShopifyFulfillmentService`
    - Open `Infrastructure/Shopify/ShopifyFulfillmentService.cs` and implement the method following the six steps in the design:
      1. Execute `GetFulfillmentOrdersWithLineItems` query with `orderId` variable
      2. Filter FulfillmentOrders to `OPEN` or `IN_PROGRESS` status
      3. For each `FulfillmentOrderLineItem` edge, check if `lineItem.id` is in the requested `lineItemGids` set
      4. Group matched `FulfillmentOrderLineItem` GIDs by parent `FulfillmentOrder` GID
      5. If no matches, throw `ShopifyFulfillmentException(["None of the requested line items were found in any open fulfillment order."])`
      6. Build `lineItemsByFulfillmentOrder` input with both `fulfillmentOrderId` AND `fulfillmentOrderLineItems: [{ id }]`, then call the existing `FulfillmentMutations.FulfillmentCreate` mutation; handle `userErrors` and log success following the same pattern as `FulfillOrderAsync`
    - _Requirements: 4.2, 4.3, 4.4, 4.5, 4.6, 10.4, 10.5_

  - [ ]* 3.3 Write unit tests for `ShopifyFulfillmentService.FulfillLineItemsAsync`
    - In the test project under `Tests/Infrastructure/Shopify/ShopifyFulfillmentServiceTests.cs`, write xUnit tests covering:
      - Correct GraphQL query sent with order GID variable
      - Line items matched correctly across multiple FulfillmentOrders
      - Grouping produces correct `lineItemsByFulfillmentOrder` structure
      - No matching line items throws `ShopifyFulfillmentException` without calling mutation
      - Shopify `userErrors` throws `ShopifyFulfillmentException`
      - Successful mutation returns `FulfillmentCreatePayload`
    - _Requirements: 4.2, 4.3, 4.4, 4.5, 4.6_

  - [ ]* 3.4 Write property test for line item GID matching and grouping (Property 3)
    - In `Tests/Properties/PartialFulfillmentProperties.cs`, write an FsCheck property test:
      - `// Feature: partial-order-fulfillment, Property 3: Line item GID matching and grouping preserves all requested items`
      - Generate random sets of `lineItemGids` (valid GID strings) and random `FulfillmentOrder` structures where some `FulfillmentOrderLineItem` nodes reference those GIDs
      - Extract the matching and grouping logic from `ShopifyFulfillmentService` into a testable static helper or test it via the service with a mocked `GraphService`
      - Verify every matched GID appears exactly once in the resulting `lineItemsByFulfillmentOrder` list, grouped under the correct `FulfillmentOrder` GID
      - Run minimum 100 iterations
    - _Requirements: 4.3, 4.4_

- [x] 4. Create the CQRS command, validator, and result record
  - [x] 4.1 Create `FulfillOrderLineItemsCommand` and `FulfillOrderLineItemsResult`
    - Create `Features/Orders/Commands/FulfillOrderLineItemsCommand.cs` with the `FulfillOrderLineItemsCommand` record (implementing `IRequest<FulfillOrderLineItemsResult>`) and the `FulfillOrderLineItemsResult` record, exactly as specified in the design
    - _Requirements: 1.3, 2.1, 2.2, 2.3, 2.4, 7.1, 7.2_

  - [x] 4.2 Create `FulfillOrderLineItemsCommandValidator`
    - Create `Features/Orders/Commands/FulfillOrderLineItemsCommandValidator.cs` extending `AbstractValidator<FulfillOrderLineItemsCommand>` with three rules:
      - `RuleFor(x => x.OrderId).NotEmpty()`
      - `RuleFor(x => x.LineItemIds).NotNull().NotEmpty()`
      - `RuleFor(x => x.LineItemIds).ForEach(rule => rule.Must(id => !string.IsNullOrWhiteSpace(id)))`
    - _Requirements: 2.5, 2.6, 2.7_

  - [ ]* 4.3 Write unit tests for `FulfillOrderLineItemsCommandValidator`
    - In `Tests/Features/Orders/FulfillOrderLineItemsCommandValidatorTests.cs`, write xUnit tests covering:
      - Valid command passes validation
      - Empty `OrderId` fails validation
      - Null `LineItemIds` fails validation
      - Empty `LineItemIds` list fails validation
      - `LineItemIds` with one blank entry fails validation
      - `LineItemIds` with all valid entries passes validation
    - _Requirements: 2.5, 2.6, 2.7_

  - [ ]* 4.4 Write property test for validator whitespace rejection (Property 2)
    - In `Tests/Properties/PartialFulfillmentProperties.cs`, add an FsCheck property test:
      - `// Feature: partial-order-fulfillment, Property 2: Validator rejects all whitespace-only inputs`
      - Generate random strings composed entirely of whitespace characters (spaces, tabs, newlines) using a custom `Arbitrary<string>` that produces only whitespace strings
      - Verify `FulfillOrderLineItemsCommandValidator` returns `IsValid = false` for commands with a whitespace `OrderId` and for commands where any entry in `LineItemIds` is whitespace
      - Run minimum 100 iterations
    - _Requirements: 2.5, 2.7_

- [x] 5. Implement `FulfillOrderLineItemsCommandHandler`
  - [x] 5.1 Create `FulfillOrderLineItemsCommandHandler` with order and line item ID resolution
    - Create `Features/Orders/Commands/FulfillOrderLineItemsCommandHandler.cs` implementing `IRequestHandler<FulfillOrderLineItemsCommand, FulfillOrderLineItemsResult>`
    - Inject `IOrderRepository`, `IFulfillmentRepository`, `IShopifyFulfillmentService`, and `ILogger<FulfillOrderLineItemsCommandHandler>`
    - Implement order GID resolution using the same three-step fallback as `FulfillOrderCommandHandler` (local int Id → `ShopifyGidHelper.BuildOrderGid` → raw GID)
    - Implement the line item ID resolution loop: for each `lineItemId`, try `int.TryParse` → match `OrderLineItem.Id` then `OrderLineItem.NumericId`; if starts with `gid://shopify/` → match `OrderLineItem.ShopifyGid`; otherwise try numeric parse then treat as raw GID; log a warning for unresolved IDs; throw `ShopifyFulfillmentException` if none resolved
    - _Requirements: 1.2, 3.1, 3.2, 3.3, 3.4, 10.3_

  - [ ]* 5.2 Write property test for order ID resolution (Property 1)
    - In `Tests/Properties/PartialFulfillmentProperties.cs`, add an FsCheck property test:
      - `// Feature: partial-order-fulfillment, Property 1: Order ID resolution handles all supported formats`
      - Generate random `orderId` values in three formats: random positive `int` as string, random positive `long` as string, and random `gid://shopify/Order/{n}` string
      - Mock `IOrderRepository.GetByAnyIdAsync` to return a stub `Order` for each format
      - Verify the handler resolves to a non-null, non-empty order GID string without throwing
      - Run minimum 100 iterations
    - _Requirements: 1.2, 3.1, 3.2_

  - [x] 5.3 Implement Shopify call, persistence, and status determination in the handler
    - Continue in `FulfillOrderLineItemsCommandHandler.cs`:
      - Call `_shopifyFulfillment.FulfillLineItemsAsync(orderGid, resolvedGids, ...)` with tracking and notification parameters
      - If order is found locally: build a `Fulfillment` entity with all required fields plus `FulfilledLineItemGids = JsonSerializer.Serialize(resolvedGids)`, call `_fulfillments.UpsertAsync`, then call `_fulfillments.GetAllForOrderAsync` to reload all fulfillments
      - Determine status: deserialize `FulfilledLineItemGids` from each record, collect into a `HashSet<string>`, compare `fulfilledGids.Count` against `order.LineItems.Count`; set `"fulfilled"` if `>=`, `"partial"` otherwise
      - Update `order.FulfillmentStatus` and `order.UpdatedAt`, call `_orders.UpsertAsync`
      - If order not found locally: log warning, skip persistence
      - Log informational message on success; return `FulfillOrderLineItemsResult`
    - _Requirements: 4.1, 5.1, 5.2, 5.3, 5.4, 5.5, 6.1, 6.2, 6.3, 6.4, 7.1, 7.3, 10.1, 10.2_

  - [ ]* 5.4 Write property test for fulfillment status determination (Property 4)
    - In `Tests/Properties/PartialFulfillmentProperties.cs`, add an FsCheck property test:
      - `// Feature: partial-order-fulfillment, Property 4: Fulfillment status determination is correct for all line item coverage ratios`
      - Generate random orders with N (1..20) line items and M (0..N) distinct fulfilled GIDs stored across fulfillment records using `Gen.Choose(1, 20)` for N and `Gen.Choose(0, N)` for M
      - Exercise the status determination logic directly (extract to a static helper or test via the handler with mocked dependencies)
      - Verify the result is `"fulfilled"` when M ≥ N and `"partial"` when M < N
      - Run minimum 100 iterations
    - _Requirements: 5.3, 5.4, 6.2, 6.3, 6.4_

  - [ ]* 5.5 Write property test for result field mapping (Property 5)
    - In `Tests/Properties/PartialFulfillmentProperties.cs`, add an FsCheck property test:
      - `// Feature: partial-order-fulfillment, Property 5: Result and entity fields are fully populated from Shopify response`
      - Generate random `ShopifyFulfillmentNode` instances with varying non-null `Id`, `Status`, and `TrackingInfo` list lengths (0..5) using a custom `Arbitrary<ShopifyFulfillmentNode>`
      - Mock all dependencies so the handler reaches the result-building step
      - Verify `FulfillOrderLineItemsResult.FulfillmentGid == shopifyFulfillment.Id`, `Status` is populated, and tracking fields use the first `TrackingInfo` entry when present and null otherwise
      - Run minimum 100 iterations
    - _Requirements: 7.1, 7.3_

  - [ ]* 5.6 Write unit tests for `FulfillOrderLineItemsCommandHandler`
    - In `Tests/Features/Orders/FulfillOrderLineItemsCommandHandlerTests.cs`, write xUnit tests covering:
      - Happy path: all IDs resolve, Shopify returns success, fulfillment persisted, status updated
      - Order not found locally: warning logged, no persistence, result still returned
      - All line item IDs unresolvable: `ShopifyFulfillmentException` thrown
      - Mixed resolvable/unresolvable IDs: warning logged for unresolvable, processing continues
      - Status set to `"partial"` when not all line items fulfilled
      - Status set to `"fulfilled"` when all line items fulfilled
      - Tracking info: first entry used when list has multiple entries
      - `GetAllForOrderAsync` called after `UpsertAsync` (verify mock call order)
    - _Requirements: 3.3, 3.4, 5.1, 5.2, 5.3, 5.4, 5.5, 6.1, 6.2, 6.3, 6.4, 7.3, 10.1, 10.2, 10.3_

- [~] 6. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Add the `PartialFulfillmentRequest` DTO and wire the controller endpoint
  - [x] 7.1 Create `PartialFulfillmentRequest` DTO
    - Create `DTOs/PartialFulfillmentRequest.cs` with the four properties defined in the design: `LineItemIds`, `TrackingNumber`, `TrackingCompany`, and `NotifyCustomer` (defaulting to `true`)
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 7.2 Add `FulfillOrderLineItems` action to `OrdersController`
    - Open `API/Controllers/OrdersController.cs` and add the `[HttpPost("{orderId}/fulfill-items")]` action that maps `PartialFulfillmentRequest` to `FulfillOrderLineItemsCommand`, sends it via `_mediator`, and returns `Accepted(result)`
    - Verify the existing `FulfillOrder` action and its route `{orderId}/fulfill` are unchanged
    - _Requirements: 1.1, 1.3, 1.4, 1.5, 8.1, 8.3_

  - [ ]* 7.3 Write integration tests for the new endpoint
    - In `Tests/Integration/OrdersControllerIntegrationTests.cs`, write xUnit integration tests using `WebApplicationFactory` with mocked Shopify GraphQL responses covering:
      - `POST /api/orders/{orderId}/fulfill-items` returns 202 with valid request
      - `POST /api/orders/{orderId}/fulfill-items` returns 400 with missing `lineItemIds`
      - `POST /api/orders/{orderId}/fulfill` (existing endpoint) continues to return 202 — non-regression
      - Both endpoints route independently when called in the same session
      - Fulfillment record is persisted with correct `FulfilledLineItemGids` JSON after a successful call
      - `Order.FulfillmentStatus` is `"partial"` after fulfilling a subset of line items
      - `Order.FulfillmentStatus` is `"fulfilled"` after fulfilling all line items
    - _Requirements: 1.1, 1.3, 1.4, 5.1, 5.3, 5.4, 8.1, 8.3_

- [x] 8. Set up the test project (prerequisite for all test sub-tasks)
  - Create a new xUnit test project `Shopify_Learning.Tests` in the solution:
    - Run `dotnet new xunit -n Shopify_Learning.Tests` from the solution root
    - Add it to the solution: `dotnet sln add Shopify_Learning.Tests/Shopify_Learning.Tests.csproj`
    - Add a project reference to the main project: `dotnet add Shopify_Learning.Tests reference Shopify_Learning/Shopify_Learning.csproj`
    - Add NuGet packages: `FsCheck.Xunit` (latest stable), `Moq` (latest stable), `Microsoft.AspNetCore.Mvc.Testing` (matching `net10.0`)
    - Create the directory structure: `Tests/Features/Orders/`, `Tests/Infrastructure/Shopify/`, `Tests/Properties/`, `Tests/Integration/`
  - _Requirements: (testing infrastructure)_

- [~] 9. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

---

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Task 8 (test project setup) must be completed before any `*` test sub-tasks are executed; it is placed last in the task list but appears in wave 0 of the dependency graph so it can be set up in parallel with the domain changes
- `FulfilledLineItemGids` is serialized/deserialized using `System.Text.Json.JsonSerializer` (already available in .NET 10, no new dependency needed)
- `OrderRepository.GetByAnyIdAsync` already includes `LineItems` via `.Include(o => o.LineItems)` — no repository change is needed for the line item count comparison
- The existing `GlobalExceptionMiddleware` handles `ShopifyFulfillmentException` — no middleware changes are needed
- Property tests use `FsCheck.Xunit` attributes (`[Property]`) and run a minimum of 100 iterations by default; increase via `[Property(MaxTest = 500)]` if desired
- Each property test file includes the comment tag `// Feature: partial-order-fulfillment, Property N: ...` for traceability

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1", "4.1", "8"] },
    { "id": 1, "tasks": ["1.2", "2.2", "4.2", "3.1"] },
    { "id": 2, "tasks": ["1.3", "3.2", "4.3", "4.4"] },
    { "id": 3, "tasks": ["3.3", "3.4", "5.1", "7.1"] },
    { "id": 4, "tasks": ["5.2", "5.3"] },
    { "id": 5, "tasks": ["5.4", "5.5", "5.6", "7.2"] },
    { "id": 6, "tasks": ["7.3"] }
  ]
}
```
