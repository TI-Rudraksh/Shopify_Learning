# Implementation Plan

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Mutation Response Data Wrapper Is Always Null
  - **CRITICAL**: This test MUST FAIL on unfixed code — failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior — it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate that `Data` is always `null` when deserializing already-unwrapped mutation responses
  - **Scoped PBT Approach**: For each of the three deterministic cases, scope the property to the concrete failing JSON payloads (already-unwrapped by `PostAsync<T>`)
  - Add a test project (e.g., `ShopifyIntegration.Tests`) targeting `net10.0` with `xunit` and `FsCheck.Xunit` (or `CsCheck`) for property-based testing
  - Write three deserialization tests (one per affected response type):
    - Deserialize `{"productCreate":{"product":{"id":"gid://shopify/Product/1","title":"Widget"},"userErrors":[]}}` into `CreateProductResponse` — assert `result.ProductCreate` is NOT null (will FAIL on unfixed code because `ProductCreate` does not exist at the top level)
    - Deserialize `{"productUpdate":{"product":{"id":"gid://shopify/Product/1","title":"Updated"},"userErrors":[]}}` into `UpdateProductResponse` — assert `result.ProductUpdate` is NOT null (will FAIL on unfixed code)
    - Deserialize `{"productDelete":{"deletedProductId":"gid://shopify/Product/1","userErrors":[]}}` into `DeleteProductResponse` — assert `result.ProductDelete` is NOT null (will FAIL on unfixed code)
  - Also assert that `result.Data` IS null on unfixed code to confirm the root cause (the `Data` property receives nothing because the key no longer exists after envelope unwrapping)
  - Run tests on UNFIXED code
  - **EXPECTED OUTCOME**: Tests FAIL (this is correct — it proves the bug exists)
  - Document counterexamples found (e.g., "`CreateProductResponse.Data` is null; `ProductCreate` property does not exist at top level")
  - Mark task complete when tests are written, run, and failure is documented
  - _Requirements: 1.1, 1.2, 1.3_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - GetProductsResponse Deserialization Is Unaffected
  - **IMPORTANT**: Follow observation-first methodology
  - Observe: on unfixed code, deserializing `{"products":{"edges":[{"node":{"id":"gid://shopify/Product/1","title":"Widget"}}]}}` into `GetProductsResponse` correctly populates `Products.Edges` with one entry
  - Observe: on unfixed code, deserializing an empty edges list `{"products":{"edges":[]}}` into `GetProductsResponse` correctly returns an empty `Edges` list
  - Write a property-based test: for all randomly generated lists of products (varying count 0–20, varying titles including null/empty, varying null nodes), deserializing the corresponding JSON into `GetProductsResponse` always yields `Products.Edges.Count` equal to the generated count and each non-null node's `Title` matching the generated value (from Preservation Requirements in design)
  - Use `FsCheck` or `CsCheck` to generate arbitrary `GetProductsResponse`-shaped JSON payloads
  - Verify tests PASS on UNFIXED code (confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.3, 3.4_

- [x] 3. Fix — Remove intermediate Data wrapper from mutation response models

  - [x] 3.1 Fix `CreateProductResponse` — remove `ProductCreateData` wrapper
    - Open `ShopifyIntegration/GraphQL/Responses/Products/CreateProductResponse.cs`
    - Delete the `ProductCreateData` class entirely
    - Replace `public ProductCreateData? Data { get; set; }` on `CreateProductResponse` with `public ProductCreatePayload? ProductCreate { get; set; }`
    - Resulting class: `public class CreateProductResponse { public ProductCreatePayload? ProductCreate { get; set; } }`
    - _Bug_Condition: isBugCondition(CreateProductResponse) — has intermediate `Data` wrapper of type `ProductCreateData` that mismatches the already-unwrapped `PostAsync<T>` payload_
    - _Expected_Behavior: result.ProductCreate IS NOT NULL AND result.ProductCreate.Product IS NOT NULL for a successful create mutation_
    - _Preservation: CreateProductAsync mutation execution and variable construction in ShopifyGraphQLService are unchanged; only the response model structure changes_
    - _Requirements: 2.1_

  - [x] 3.2 Fix `UpdateProductResponse` — remove `UpdateProductData` wrapper
    - Open `ShopifyIntegration/GraphQL/Responses/Products/UpdateProductResponse.cs`
    - Delete the `UpdateProductData` class entirely
    - Replace `public UpdateProductData? Data { get; set; }` on `UpdateProductResponse` with `public ProductUpdatePayload? ProductUpdate { get; set; }`
    - Remove the `using UserError = ShopifySharp.GraphQL.UserError;` alias if it is no longer needed after removing the wrapper class (verify `ProductUpdatePayload` still references `UserError` correctly)
    - Resulting class: `public class UpdateProductResponse { public ProductUpdatePayload? ProductUpdate { get; set; } }`
    - _Bug_Condition: isBugCondition(UpdateProductResponse) — has intermediate `Data` wrapper of type `UpdateProductData`_
    - _Expected_Behavior: result.ProductUpdate IS NOT NULL AND result.ProductUpdate.Product IS NOT NULL for a successful update mutation_
    - _Preservation: UpdateProductAsync mutation execution and variable construction in ShopifyGraphQLService are unchanged_
    - _Requirements: 2.2_

  - [x] 3.3 Fix `DeleteProductResponse` — remove `DeleteProductData` wrapper
    - Open `ShopifyIntegration/GraphQL/Responses/Products/DeleteProductResponse.cs`
    - Delete the `DeleteProductData` class entirely
    - Replace `public DeleteProductData? Data { get; set; }` on `DeleteProductResponse` with `public ProductDeletePayload? ProductDelete { get; set; }`
    - Resulting class: `public class DeleteProductResponse { public ProductDeletePayload? ProductDelete { get; set; } }`
    - _Bug_Condition: isBugCondition(DeleteProductResponse) — has intermediate `Data` wrapper of type `DeleteProductData`_
    - _Expected_Behavior: result.ProductDelete IS NOT NULL AND result.ProductDelete.DeletedProductId IS NOT NULL for a successful delete mutation_
    - _Preservation: DeleteProductAsync mutation execution and variable construction in ShopifyGraphQLService are unchanged_
    - _Requirements: 2.3_

  - [x] 3.4 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Mutation Payload Accessible Directly on Response
    - **IMPORTANT**: Re-run the SAME tests from task 1 — do NOT write new tests
    - The tests from task 1 assert `result.ProductCreate`, `result.ProductUpdate`, and `result.ProductDelete` are NOT null
    - When these tests pass, it confirms the expected behavior from Requirements 2.1, 2.2, 2.3 is satisfied
    - Run all three bug condition exploration tests from step 1
    - **EXPECTED OUTCOME**: All three tests PASS (confirms bug is fixed for create, update, and delete)
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 3.5 Verify preservation tests still pass
    - **Property 2: Preservation** - GetProductsResponse Deserialization Is Unaffected
    - **IMPORTANT**: Re-run the SAME tests from task 2 — do NOT write new tests
    - Run the `GetProductsResponse` property-based preservation tests from step 2
    - **EXPECTED OUTCOME**: All preservation tests PASS (confirms no regressions in query deserialization)
    - Confirm `GetProductsResponse` model was not modified and all generated inputs still deserialize correctly
    - _Requirements: 3.1, 3.3, 3.4_

- [x] 4. Checkpoint — Ensure all tests pass
  - Run the full test suite for `ShopifyIntegration.Tests`
  - Confirm Property 1 (bug condition) tests pass — mutation payloads are no longer null
  - Confirm Property 2 (preservation) tests pass — `GetProductsResponse` deserialization is unchanged
  - Confirm the project builds without errors or warnings (`dotnet build`)
  - Ensure all tests pass; ask the user if any questions arise
