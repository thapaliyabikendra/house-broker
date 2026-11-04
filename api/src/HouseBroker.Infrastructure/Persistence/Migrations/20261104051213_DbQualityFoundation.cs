using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseBroker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DbQualityFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Listings_City_Locality",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_Price",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_PropertyTypeId",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_Status",
                table: "Listings");

            migrationBuilder.AlterDatabase(
                collation: "Latin1_General_100_CI_AI");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_City_Locality",
                table: "Listings",
                columns: new[] { "City", "Locality" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Price",
                table: "Listings",
                column: "Price",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_PropertyTypeId",
                table: "Listings",
                column: "PropertyTypeId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Status",
                table: "Listings",
                column: "Status",
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Listings_City_Locality",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_Price",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_PropertyTypeId",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_Status",
                table: "Listings");

            migrationBuilder.AlterDatabase(
                oldCollation: "Latin1_General_100_CI_AI");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_City_Locality",
                table: "Listings",
                columns: new[] { "City", "Locality" });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Price",
                table: "Listings",
                column: "Price");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_PropertyTypeId",
                table: "Listings",
                column: "PropertyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Status",
                table: "Listings",
                column: "Status");
        }
    }
}
