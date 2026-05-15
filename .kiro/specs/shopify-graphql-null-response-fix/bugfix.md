# Bugfix Requirements Document

## Introduction

`ShopifyGraphQLService` uses `ShopifySharp`'s `GraphService.PostAsync<T>()`, which automatically unwraps the top-level GraphQL `data` envelope before deserializing into `T`. After unwrapping, the JSON for a mutation response looks like:

```json
{ "productCreate": { "product": { ... }, "userErrors": [] } }
```

However, `CreateProductResponse`, `UpdateProductResponse`, and `DeleteProductResponse` each contain an extra intermediate `Data` property (e.g., `public ProductCreateData? Data { get; set; }`). This causes the deserializer to look for a second `data` key inside the already-unwrapped payload — a key that does not exist — so the entire response is always `null`.

The result is that mutation operations (create, update, delete) succeed in Shopify but the service always returns `null` to the caller, making it impossible to confirm success, retrieve the created/updated product, or surface user errors.

`GetProductsResponse` does not have this extra wrapper and is correctly structured.

---

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN `CreateProductAsync` is called with valid input THEN the system deserializes the GraphQL response into `CreateProductResponse` where `Data` is always `null` because the deserializer searches for a `"data"` key that no longer exists after `PostAsync<T>` has already unwrapped the envelope.

1.2 WHEN `UpdateProductAsync` is called with valid input THEN the system deserializes the GraphQL response into `UpdateProductResponse` where `Data` is always `null` for the same reason.

1.3 WHEN `DeleteProductAsync` is called with a valid product ID THEN the system deserializes the GraphQL response into `DeleteProductResponse` where `Data` is always `null` for the same reason.

### Expected Behavior (Correct)

2.1 WHEN `CreateProductAsync` is called with valid input THEN the system SHALL deserialize the response directly into `CreateProductResponse.ProductCreate` (of type `ProductCreatePayload`), exposing the created product and any user errors without an intermediate `Data` wrapper.

2.2 WHEN `UpdateProductAsync` is called with valid input THEN the system SHALL deserialize the response directly into `UpdateProductResponse.ProductUpdate` (of type `ProductUpdatePayload`), exposing the updated product and any user errors without an intermediate `Data` wrapper.

2.3 WHEN `DeleteProductAsync` is called with a valid product ID THEN the system SHALL deserialize the response directly into `DeleteProductResponse.ProductDelete` (of type `ProductDeletePayload`), exposing the deleted product ID and any user errors without an intermediate `Data` wrapper.

### Unchanged Behavior (Regression Prevention)

3.1 WHEN `GetProductsAsync` is called THEN the system SHALL CONTINUE TO deserialize the response into `GetProductsResponse.Products` (of type `ProductsConnection`) and return the product list correctly, as this response model has no extra wrapper and is already correct.

3.2 WHEN any mutation is called and Shopify returns user errors THEN the system SHALL CONTINUE TO surface those errors through the `UserErrors` list on the respective payload object.

3.3 WHEN any mutation is called and Shopify returns a network or HTTP error THEN the system SHALL CONTINUE TO propagate that exception to the caller unchanged.

3.4 WHEN `CreateProductAsync` is called with valid input THEN the system SHALL CONTINUE TO successfully create the product in Shopify (the mutation itself is not affected — only the response deserialization is fixed).

---

## Bug Condition

**Bug Condition Function:**

```pascal
FUNCTION isBugCondition(X)
  INPUT: X of type GraphQL mutation response type
  OUTPUT: boolean

  // Returns true when the response model has an extra Data wrapper
  // that mismatches the already-unwrapped PostAsync<T> payload
  RETURN X is one of {CreateProductResponse, UpdateProductResponse, DeleteProductResponse}
         AND X has a property named Data of an intermediate wrapper type
END FUNCTION
```

**Property: Fix Checking**

```pascal
// Property: Fix Checking — Mutation responses deserialize correctly
FOR ALL X WHERE isBugCondition(X) DO
  result ← ExecuteAsync<X>(mutation, variables)
  ASSERT result.MutationPayload IS NOT NULL
         AND result.MutationPayload.Product IS NOT NULL (for create/update)
         OR result.MutationPayload.DeletedProductId IS NOT NULL (for delete)
END FOR
```

**Property: Preservation Checking**

```pascal
// Property: Preservation Checking — Non-buggy responses are unaffected
FOR ALL X WHERE NOT isBugCondition(X) DO
  ASSERT ExecuteAsync<X>(query, variables) behaves identically before and after the fix
END FOR
// Specifically: GetProductsResponse continues to return products correctly
```
