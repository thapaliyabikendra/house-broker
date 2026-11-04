using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseBroker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QueryOptimizationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Listings_BrokerId",
                table: "Listings",
                column: "BrokerId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Status_CreatedAt",
                table: "Listings",
                columns: new[] { "Status", "CreatedAt" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionRates_IsActive_EffectiveFrom",
                table: "CommissionRates",
                columns: new[] { "IsActive", "EffectiveFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Listings_BrokerId",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_Status_CreatedAt",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_CommissionRates_IsActive_EffectiveFrom",
                table: "CommissionRates");
        }
    }
}
