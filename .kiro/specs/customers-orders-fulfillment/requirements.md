# Requirements Document

## Introduction

This feature adds Customers, Orders, and full Order Fulfillment support to the existing .NET 10 Shopify integration project. It follows the same CQRS + Vertical Slice Architecture with MediatR, EF Core (snake_case PostgreSQL), FluentValidation, and custom Shopify GraphQL services already established by the Products, Inventory, Locations, and Webhooks features.

The implementation is scoped to **Part 1**: Domain Entities → EF Core Configurations → Repositories → ShopifyFulfillmentService + GraphQL models. No CQRS feature slices (commands/queries/controllers) are included in this scope.

## Glossary

- **Customer**: A Shopify customer record synced to the local PostgreSQL database.
- **Order**: A Shopify order record, optionally linked to a Customer, containing line items and fulfillments.
- **OrderLineItem**: A single product line within an Order.
- **Fulfillment**: A Shopify fulfillment record linked to an Order, carrying tracking information.
- **ShopifyGid**: A Shopify Global ID string (e.g., `gid://shopify/Order/12345`).
- **NumericId**: The numeric portion extracted from a ShopifyGid.
- **FulfillmentOrder**: A Shopify concept grouping line items that can be fulfilled together; referenced by GID.
- **ShopifyDbContext**: The EF Core `DbContext` for this application.
- **Repository**: A data-access abstraction over `ShopifyDbContext` following the existing `IProductRepository` / `IInventoryRepository` pattern.
- **ShopifyFulfillmentService**: The service responsible for calling Shopify's GraphQL API to create fulfillments.
- **FulfillmentCreateResult**: The result DTO returned by `ShopifyFulfillmentService.FulfillOrderAsync`.
- **UserError**: A Shopify GraphQL `userErrors` entry containing a field path and message.
- **ShopifyFulfillmentException**: A domain exception thrown when Shopify returns one or more `userErrors` during a fulfillment mutation.
- **xmin**: PostgreSQL system column used as an optimistic-concurrency token via EF Core's `IsRowVersion()`.

---

## Requirements

### Requirement 1: Customer Domain Entity

**User Story:** As a developer, I want a `Customer` entity that mirrors the Shopify customer object, so that customer data can be persisted and queried locally.

#### Acceptance Criteria

1. THE `Customer` entity SHALL expose the following properties: `Id` (int, PK), `ShopifyGid` (string), `NumericId` (long), `Email` (string), `FirstName` (string), `LastName` (string), `Phone` (string, nullable), `AcceptsMarketing` (bool), `CreatedAt` (DateTimeOffset), `UpdatedAt` (DateTimeOffset).
2. THE `Customer` entity SHALL be declared as a `sealed class` in `Domain/Entities/Customer.cs` within the `ShopifyIntegration.Domain.Entities` namespace, matching the style of `Product` and `InventoryLevel`.
3. THE `Customer` entity SHALL initialise all non-nullable string properties to `""` to satisfy C# nullable-reference-type analysis, consistent with existing entities.

---

### Requirement 2: Order Domain Entity

**User Story:** As a developer, I want an `Order` entity that mirrors the Shopify order object, so that order data including financial status, fulfilment status, and relationships to customers and line items can be persisted locally.

#### Acceptance Criteria

1. THE `Order` entity SHALL expose the following properties: `Id` (int, PK), `ShopifyGid` (string), `NumericId` (long), `Name` (string, e.g. `#1001`), `FinancialStatus` (string), `FulfillmentStatus` (string), `TotalPrice` (decimal), `Currency` (string), `CustomerId` (int, nullable FK), `Customer` (navigation property, nullable), `LineItems` (ICollection\<OrderLineItem\>), `Fulfillments` (ICollection\<Fulfillment\>), `CreatedAt` (DateTimeOffset), `UpdatedAt` (DateTimeOffset), `CancelledAt` (DateTimeOffset, nullable).
2. THE `Order` entity SHALL be declared as a `sealed class` in `Domain/Entities/Order.cs` within the `ShopifyIntegration.Domain.Entities` namespace.
3. THE `Order` entity SHALL initialise collection navigation properties (`LineItems`, `Fulfillments`) to empty collections so that callers never encounter a null reference.

---

### Requirement 3: OrderLineItem Domain Entity

**User Story:** As a developer, I want an `OrderLineItem` entity that represents a single product line within an order, so that per-line pricing, quantity, and SKU data can be stored and queried.

#### Acceptance Criteria

1. THE `OrderLineItem` entity SHALL expose the following properties: `Id` (int, PK), `OrderId` (int, FK), `Order` (navigation property), `ShopifyGid` (string), `NumericId` (long), `Title` (string), `VariantTitle` (string), `Quantity` (int), `Price` (decimal), `Sku` (string), `ProductGid` (string), `VariantGid` (string).
2. THE `OrderLineItem` entity SHALL be declared as a `sealed class` in `Domain/Entities/OrderLineItem.cs` within the `ShopifyIntegration.Domain.Entities` namespace.
3. THE `OrderLineItem` entity SHALL initialise all non-nullable string properties to `""`.

---

### Requirement 4: Fulfillment Domain Entity

**User Story:** As a developer, I want a `Fulfillment` entity that records the outcome of fulfilling an order, including tracking details, so that fulfilment state can be persisted and surfaced to consumers.

#### Acceptance Criteria

1. THE `Fulfillment` entity SHALL expose the following properties: `Id` (int, PK), `ShopifyGid` (string), `NumericId` (long), `OrderId` (int, FK), `Order` (navigation property), `Status` (string), `TrackingNumber` (string, nullable), `TrackingCompany` (string, nullable), `TrackingUrl` (string, nullable), `FulfillmentOrderGid` (string, nullable), `CreatedAt` (DateTimeOffset), `UpdatedAt` (DateTimeOffset).
2. THE `Fulfillment` entity SHALL be declared as a `sealed class` in `Domain/Entities/Fulfillment.cs` within the `ShopifyIntegration.Domain.Entities` namespace.
3. THE `Fulfillment` entity SHALL initialise all non-nullable string properties to `""`.

---

### Requirement 5: EF Core Configuration — Customer

**User Story:** As a developer, I want a `CustomerConfiguration` class that maps the `Customer` entity to a snake_case PostgreSQL table, so that the database schema is consistent with the existing conventions.

#### Acceptance Criteria

1. THE `CustomerConfiguration` SHALL map the `Customer` entity to the `customers` table with all column names in snake_case (e.g., `shopify_gid`, `numeric_id`, `first_name`, `last_name`, `accepts_marketing`, `created_at`, `updated_at`).
2. THE `CustomerConfiguration` SHALL declare a unique index on `shopify_gid`.
3. THE `CustomerConfiguration` SHALL configure `id` with `UseIdentityAlwaysColumn()`, consistent with existing configurations.
4. THE `CustomerConfiguration` SHALL be placed in `Infrastructure/Data/Configurations/CustomerConfiguration.cs` and implement `IEntityTypeConfiguration<Customer>`.
5. WHEN `OnModelCreating` is called, THE `ShopifyDbContext` SHALL apply `CustomerConfiguration` automatically via `ApplyConfigurationsFromAssembly`.

---

### Requirement 6: EF Core Configuration — Order

**User Story:** As a developer, I want an `OrderConfiguration` class that maps the `Order` entity to a snake_case PostgreSQL table with proper FK relationships, so that order data is stored correctly and relational integrity is enforced.

#### Acceptance Criteria

1. THE `OrderConfiguration` SHALL map the `Order` entity to the `orders` table with all column names in snake_case (e.g., `shopify_gid`, `numeric_id`, `financial_status`, `fulfillment_status`, `total_price`, `customer_id`, `created_at`, `updated_at`, `cancelled_at`).
2. THE `OrderConfiguration` SHALL declare a unique index on `shopify_gid`.
3. THE `OrderConfiguration` SHALL configure the optional FK relationship from `Order.CustomerId` to `Customer.Id` with a named constraint `fk_orders_customers` and `OnDelete(DeleteBehavior.SetNull)`.
4. THE `OrderConfiguration` SHALL configure `id` with `UseIdentityAlwaysColumn()`.
5. THE `OrderConfiguration` SHALL apply an `xmin` concurrency token using `HasColumnType("xid").IsRowVersion()`, consistent with `InventoryLevelConfiguration`.

---

### Requirement 7: EF Core Configuration — OrderLineItem

**User Story:** As a developer, I want an `OrderLineItemConfiguration` class that maps `OrderLineItem` to a snake_case PostgreSQL table with a FK to `orders`, so that line items are correctly associated with their parent order.

#### Acceptance Criteria

1. THE `OrderLineItemConfiguration` SHALL map the `OrderLineItem` entity to the `order_line_items` table with all column names in snake_case (e.g., `order_id`, `shopify_gid`, `numeric_id`, `variant_title`, `product_gid`, `variant_gid`).
2. THE `OrderLineItemConfiguration` SHALL configure the FK relationship from `OrderLineItem.OrderId` to `Order.Id` with a named constraint `fk_order_line_items_orders` and `OnDelete(DeleteBehavior.Cascade)`.
3. THE `OrderLineItemConfiguration` SHALL configure `id` with `UseIdentityAlwaysColumn()`.

---

### Requirement 8: EF Core Configuration — Fulfillment

**User Story:** As a developer, I want a `FulfillmentConfiguration` class that maps `Fulfillment` to a snake_case PostgreSQL table with a FK to `orders`, so that fulfillment records are correctly linked to their parent order.

#### Acceptance Criteria

1. THE `FulfillmentConfiguration` SHALL map the `Fulfillment` entity to the `fulfillments` table with all column names in snake_case (e.g., `shopify_gid`, `numeric_id`, `order_id`, `tracking_number`, `tracking_company`, `tracking_url`, `fulfillment_order_gid`, `created_at`, `updated_at`).
2. THE `FulfillmentConfiguration` SHALL declare a unique index on `shopify_gid`.
3. THE `FulfillmentConfiguration` SHALL configure the FK relationship from `Fulfillment.OrderId` to `Order.Id` with a named constraint `fk_fulfillments_orders` and `OnDelete(DeleteBehavior.Cascade)`.
4. THE `FulfillmentConfiguration` SHALL configure `id` with `UseIdentityAlwaysColumn()`.
5. THE `FulfillmentConfiguration` SHALL apply an `xmin` concurrency token using `HasColumnType("xid").IsRowVersion()`.

---

### Requirement 9: ShopifyDbContext — New DbSets

**User Story:** As a developer, I want `ShopifyDbContext` to expose `DbSet` properties for the four new entities, so that EF Core can query and persist them.

#### Acceptance Criteria

1. THE `ShopifyDbContext` SHALL expose `DbSet<Customer> Customers`, `DbSet<Order> Orders`, `DbSet<OrderLineItem> OrderLineItems`, and `DbSet<Fulfillment> Fulfillments` properties.
2. WHEN `OnModelCreating` is called, THE `ShopifyDbContext` SHALL continue to use `ApplyConfigurationsFromAssembly` so that all four new configurations are picked up automatically without manual registration.

---

### Requirement 10: ICustomerRepository

**User Story:** As a developer, I want an `ICustomerRepository` interface and EF Core implementation, so that customer records can be upserted and retrieved by Shopify GID.

#### Acceptance Criteria

1. THE `ICustomerRepository` interface SHALL declare: `Task<Customer> UpsertAsync(Customer customer, CancellationToken ct = default)`, `Task<Customer?> GetByShopifyGidAsync(string shopifyGid, CancellationToken ct = default)`, `Task<Customer?> GetByNumericIdAsync(long numericId, CancellationToken ct = default)`, `Task<List<Customer>> GetAllAsync(CancellationToken ct = default)`.
2. THE `CustomerRepository` implementation SHALL upsert by matching on `ShopifyGid`: insert if not found, update mutable fields (`Email`, `FirstName`, `LastName`, `Phone`, `AcceptsMarketing`, `UpdatedAt`) if found.
3. THE `CustomerRepository` implementation SHALL be placed in `Infrastructure/Data/Repositories/CustomerRepository.cs` and follow the same constructor pattern as `ProductRepository` (injecting `ShopifyDbContext` and `ILogger<CustomerRepository>`).

---

### Requirement 11: IOrderRepository

**User Story:** As a developer, I want an `IOrderRepository` interface and EF Core implementation that supports flexible lookup by local ID, numeric ID, or Shopify GID, so that order records can be resolved regardless of which identifier is available at call time.

#### Acceptance Criteria

1. THE `IOrderRepository` interface SHALL declare: `Task<Order?> GetByAnyIdAsync(string orderId, CancellationToken ct = default)`, `Task<Order?> GetByShopifyGidAsync(string shopifyGid, CancellationToken ct = default)`, `Task<Order> UpsertAsync(Order order, CancellationToken ct = default)`, `Task<List<Order>> GetAllAsync(CancellationToken ct = default)`.
2. WHEN `GetByAnyIdAsync` is called with a value that parses as an integer, THE `OrderRepository` SHALL first attempt to find the order by local `Id`; if not found, it SHALL attempt to find by `NumericId`; if still not found, it SHALL return `null`.
3. WHEN `GetByAnyIdAsync` is called with a value that does not parse as an integer, THE `OrderRepository` SHALL treat the value as a Shopify GID and delegate to `GetByShopifyGidAsync`.
4. THE `OrderRepository` upsert SHALL match on `ShopifyGid` and update mutable fields (`Name`, `FinancialStatus`, `FulfillmentStatus`, `TotalPrice`, `Currency`, `CustomerId`, `UpdatedAt`, `CancelledAt`) when an existing record is found.
5. THE `OrderRepository` implementation SHALL include `LineItems` and `Fulfillments` navigation properties when loading orders (eager loading via `.Include()`).
6. THE `OrderRepository` implementation SHALL be placed in `Infrastructure/Data/Repositories/OrderRepository.cs` and follow the same constructor pattern as `ProductRepository`.

---

### Requirement 12: IFulfillmentRepository

**User Story:** As a developer, I want an `IFulfillmentRepository` interface and EF Core implementation, so that fulfillment records can be upserted and retrieved by Shopify GID or by order.

#### Acceptance Criteria

1. THE `IFulfillmentRepository` interface SHALL declare: `Task<Fulfillment> UpsertAsync(Fulfillment fulfillment, CancellationToken ct = default)`, `Task<Fulfillment?> GetByShopifyGidAsync(string shopifyGid, CancellationToken ct = default)`, `Task<List<Fulfillment>> GetByOrderIdAsync(int orderId, CancellationToken ct = default)`.
2. THE `FulfillmentRepository` upsert SHALL match on `ShopifyGid` and update mutable fields (`Status`, `TrackingNumber`, `TrackingCompany`, `TrackingUrl`, `UpdatedAt`) when an existing record is found.
3. THE `FulfillmentRepository` implementation SHALL be placed in `Infrastructure/Data/Repositories/FulfillmentRepository.cs` and follow the same constructor pattern as `ProductRepository`.

---

### Requirement 13: ShopifyFulfillmentException

**User Story:** As a developer, I want a `ShopifyFulfillmentException` that carries Shopify `userErrors`, so that callers can distinguish fulfillment-specific API failures from general exceptions.

#### Acceptance Criteria

1. THE `ShopifyFulfillmentException` SHALL be a `sealed class` that extends `Exception` and exposes an `IReadOnlyList<string> ShopifyErrors` property.
2. THE `ShopifyFulfillmentException` constructor SHALL accept `IEnumerable<string> errors`, store them as a read-only list, and compose a human-readable `Message` in the format `"Shopify fulfillment error(s): {joined errors}"`, consistent with `ShopifyInventoryException`.
3. THE `ShopifyFulfillmentException` SHALL be placed in `Infrastructure/Shopify/ShopifyFulfillmentException.cs`.

---

### Requirement 14: GraphQL Queries — FulfillmentOrders

**User Story:** As a developer, I want a GraphQL query that retrieves the fulfillment orders for a given Shopify order GID, so that the fulfillment service can obtain the fulfillment order IDs required by the `fulfillmentCreate` mutation.

#### Acceptance Criteria

1. THE `FulfillmentQueries` static class SHALL define a `GetFulfillmentOrders` constant containing a GraphQL query that accepts an `$orderId: ID!` variable and returns the `fulfillmentOrders` for that order, including each fulfillment order's `id` and `status`.
2. THE `FulfillmentQueries` class SHALL be placed in `GraphQL/Queries/FulfillmentQueries.cs`.

---

### Requirement 15: GraphQL Mutations — FulfillmentCreate

**User Story:** As a developer, I want a GraphQL mutation that creates a fulfillment for one or more fulfillment orders, so that the fulfillment service can trigger fulfillment on Shopify.

#### Acceptance Criteria

1. THE `FulfillmentMutations` static class SHALL define a `FulfillmentCreate` constant containing a GraphQL mutation that accepts a `$fulfillment: FulfillmentInput!` variable and calls `fulfillmentCreate`, returning the created fulfillment's `id`, `status`, `trackingInfo { number company url }`, and `userErrors { field message code }`.
2. THE `FulfillmentMutations` class SHALL be placed in `GraphQL/Mutations/FulfillmentMutations.cs`.

---

### Requirement 16: GraphQL Response POCOs — Fulfillment

**User Story:** As a developer, I want strongly-typed C# POCOs that map to the Shopify GraphQL responses for fulfillment queries and mutations, so that `System.Text.Json` / `Newtonsoft.Json` can deserialise them without reflection errors.

#### Acceptance Criteria

1. THE `GetFulfillmentOrdersResponse` POCO SHALL map the response of the `GetFulfillmentOrders` query, including a nested `FulfillmentOrderConnection` with edges/nodes containing `Id` and `Status`.
2. THE `FulfillmentCreateResponse` POCO SHALL map the response of the `FulfillmentCreate` mutation, including the created `Fulfillment` object (`Id`, `Status`, `TrackingInfo`) and `UserErrors`.
3. ALL fulfillment response POCOs SHALL be placed under `GraphQL/Responses/Fulfillments/` and follow the same naming and structure conventions as the existing `Inventory` response POCOs.
4. THE `FulfillmentUserError` POCO SHALL expose `Field` (List\<string\>?), `Message` (string?), and `Code` (string?) properties, consistent with `InventoryUserError`.

---

### Requirement 17: IShopifyFulfillmentService and ShopifyFulfillmentService

**User Story:** As a developer, I want an `IShopifyFulfillmentService` interface and `ShopifyFulfillmentService` implementation that orchestrates the two-step Shopify fulfillment flow (query fulfillment orders → create fulfillment), so that orders can be fulfilled via a single method call.

#### Acceptance Criteria

1. THE `IShopifyFulfillmentService` interface SHALL declare `Task<FulfillmentCreateResult> FulfillOrderAsync(string orderGid, string? trackingNumber, string? trackingCompany, bool notifyCustomer = true, CancellationToken ct = default)`.
2. THE `FulfillmentCreateResult` record SHALL expose `string FulfillmentGid`, `string Status`, `string? TrackingNumber`, `string? TrackingCompany`, `string? TrackingUrl`.
3. WHEN `FulfillOrderAsync` is called, THE `ShopifyFulfillmentService` SHALL first call the `GetFulfillmentOrders` query to retrieve all open fulfillment order IDs for the given `orderGid`.
4. WHEN at least one open fulfillment order is found, THE `ShopifyFulfillmentService` SHALL call the `FulfillmentCreate` mutation with all fulfillment order IDs, the provided tracking details, and the `notifyCustomer` flag.
5. IF the `FulfillmentCreate` mutation returns one or more `userErrors`, THEN THE `ShopifyFulfillmentService` SHALL throw a `ShopifyFulfillmentException` containing the error messages.
6. IF no open fulfillment orders are found for the given `orderGid`, THEN THE `ShopifyFulfillmentService` SHALL throw an `InvalidOperationException` with a descriptive message.
7. THE `ShopifyFulfillmentService` SHALL be constructed with `IConfiguration` and `ILogger<ShopifyFulfillmentService>`, following the same pattern as `ShopifyInventoryService`.
8. THE `ShopifyFulfillmentService` SHALL be placed in `Infrastructure/Shopify/ShopifyFulfillmentService.cs`.

---

### Requirement 18: Dependency Injection Registration

**User Story:** As a developer, I want all new repositories and services registered in `Program.cs`, so that they are available for injection throughout the application.

#### Acceptance Criteria

1. THE `Program.cs` SHALL register `ICustomerRepository` → `CustomerRepository`, `IOrderRepository` → `OrderRepository`, and `IFulfillmentRepository` → `FulfillmentRepository` as scoped services, consistent with existing repository registrations.
2. THE `Program.cs` SHALL register `IShopifyFulfillmentService` → `ShopifyFulfillmentService` as a scoped service, consistent with `IShopifyInventoryService` registration.
