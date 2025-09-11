using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GateSale.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubCategoryToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubCategory",
                table: "Products",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubCategory",
                table: "Products");
        }
    }
}
