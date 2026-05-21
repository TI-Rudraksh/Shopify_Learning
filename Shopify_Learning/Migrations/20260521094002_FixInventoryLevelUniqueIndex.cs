using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopifyIntegration.Migrations
{
    /// <inheritdoc />
    public partial class FixInventoryLevelUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inventory_levels_product_id_location_gid",
                table: "inventory_levels");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_levels_inventory_item_gid_location_gid",
                table: "inventory_levels",
                columns: new[] { "inventory_item_gid", "location_gid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_levels_product_id",
                table: "inventory_levels",
                column: "product_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inventory_levels_inventory_item_gid_location_gid",
                table: "inventory_levels");

            migrationBuilder.DropIndex(
                name: "IX_inventory_levels_product_id",
                table: "inventory_levels");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_levels_product_id_location_gid",
                table: "inventory_levels",
                columns: new[] { "product_id", "location_gid" },
                unique: true);
        }
    }
}
