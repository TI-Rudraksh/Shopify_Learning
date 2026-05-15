using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ShopifyIntegration.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomersOrdersFulfillments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    shopify_gid = table.Column<string>(type: "text", nullable: false),
                    numeric_id = table.Column<long>(type: "bigint", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    accepts_marketing = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    shopify_gid = table.Column<string>(type: "text", nullable: false),
                    numeric_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    financial_status = table.Column<string>(type: "text", nullable: false),
                    fulfillment_status = table.Column<string>(type: "text", nullable: false),
                    total_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    customer_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_orders_customers",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "fulfillments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    shopify_gid = table.Column<string>(type: "text", nullable: false),
                    numeric_id = table.Column<long>(type: "bigint", nullable: false),
                    order_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    tracking_number = table.Column<string>(type: "text", nullable: true),
                    tracking_company = table.Column<string>(type: "text", nullable: true),
                    tracking_url = table.Column<string>(type: "text", nullable: true),
                    fulfillment_order_gid = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fulfillments", x => x.id);
                    table.ForeignKey(
                        name: "fk_fulfillments_orders",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_line_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    order_id = table.Column<int>(type: "integer", nullable: false),
                    shopify_gid = table.Column<string>(type: "text", nullable: false),
                    numeric_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    variant_title = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    sku = table.Column<string>(type: "text", nullable: false),
                    product_gid = table.Column<string>(type: "text", nullable: false),
                    variant_gid = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_line_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_line_items_orders",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customers_shopify_gid",
                table: "customers",
                column: "shopify_gid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fulfillments_order_id",
                table: "fulfillments",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_fulfillments_shopify_gid",
                table: "fulfillments",
                column: "shopify_gid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_line_items_order_id",
                table: "order_line_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_customer_id",
                table: "orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_shopify_gid",
                table: "orders",
                column: "shopify_gid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fulfillments");

            migrationBuilder.DropTable(
                name: "order_line_items");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "customers");
        }
    }
}
