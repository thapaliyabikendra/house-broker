using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseBroker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixListingRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ListingFeatures_Listings_ListingId1",
                table: "ListingFeatures");

            migrationBuilder.DropForeignKey(
                name: "FK_ListingImages_Listings_ListingId1",
                table: "ListingImages");

            migrationBuilder.DropIndex(
                name: "IX_ListingImages_ListingId1",
                table: "ListingImages");

            migrationBuilder.DropIndex(
                name: "IX_ListingFeatures_ListingId1",
                table: "ListingFeatures");

            migrationBuilder.DropColumn(
                name: "ListingId1",
                table: "ListingImages");

            migrationBuilder.DropColumn(
                name: "ListingId1",
                table: "ListingFeatures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ListingId1",
                table: "ListingImages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ListingId1",
                table: "ListingFeatures",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingImages_ListingId1",
                table: "ListingImages",
                column: "ListingId1");

            migrationBuilder.CreateIndex(
                name: "IX_ListingFeatures_ListingId1",
                table: "ListingFeatures",
                column: "ListingId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ListingFeatures_Listings_ListingId1",
                table: "ListingFeatures",
                column: "ListingId1",
                principalTable: "Listings",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ListingImages_Listings_ListingId1",
                table: "ListingImages",
                column: "ListingId1",
                principalTable: "Listings",
                principalColumn: "Id");
        }
    }
}
