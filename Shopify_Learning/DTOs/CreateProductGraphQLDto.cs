namespace ShopifyIntegration.DTOs;

public class CreateProductGraphQLDto
{
    public string Title { get; set; }

    public string DescriptionHtml { get; set; }

    public string Vendor { get; set; }
}