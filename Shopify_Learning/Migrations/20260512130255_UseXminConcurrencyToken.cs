using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopifyIntegration.Migrations
{
    /// <inheritdoc />
    public partial class UseXminConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "row_version",
                table: "inventory_levels");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "inventory_levels",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "inventory_levels");

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "inventory_levels",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
