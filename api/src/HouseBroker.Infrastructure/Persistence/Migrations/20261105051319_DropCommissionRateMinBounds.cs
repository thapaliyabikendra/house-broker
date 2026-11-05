using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HouseBroker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropCommissionRateMinBounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy rows stored both bounds. Validate the dual-bound set before dropping the lower bound so a gap
            // or overlap in existing data aborts the migration instead of silently changing pricing semantics.
            // A fresh install has no rows and skips the guard.
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [CommissionRates])
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [CommissionRates] WHERE [IsActive] = 1)
                        ;THROW 51001, 'CommissionRates migration aborted: no active commission band exists.', 1;

                    IF (SELECT COUNT(*) FROM [CommissionRates] WHERE [IsActive] = 1 AND [MaxPrice] IS NULL) <> 1
                        ;THROW 51002, 'CommissionRates migration aborted: the active set must have exactly one open-ended top band.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM [CommissionRates] AS [open]
                        INNER JOIN [CommissionRates] AS [bounded]
                            ON [bounded].[IsActive] = 1 AND [bounded].[MaxPrice] IS NOT NULL
                        WHERE [open].[IsActive] = 1 AND [open].[MaxPrice] IS NULL
                          AND [bounded].[MinPrice] >= [open].[MinPrice])
                        ;THROW 51003, 'CommissionRates migration aborted: the open-ended band is not the top band.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM [CommissionRates] AS [r]
                        WHERE [r].[IsActive] = 1
                          AND [r].[MinPrice] = (SELECT MIN([MinPrice]) FROM [CommissionRates] WHERE [IsActive] = 1)
                          AND ([r].[MinPrice] <> 0 OR [r].[MinInclusive] = 0))
                        ;THROW 51004, 'CommissionRates migration aborted: the lowest active band must start at zero inclusive.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM [CommissionRates] AS [r]
                        WHERE [r].[IsActive] = 1 AND [r].[MaxPrice] IS NOT NULL
                          AND (
                              [r].[MaxPrice] <= [r].[MinPrice]
                              OR (SELECT COUNT(*) FROM [CommissionRates] AS [n]
                                  WHERE [n].[IsActive] = 1
                                    AND [n].[MinPrice] = [r].[MaxPrice]
                                    AND [n].[MinInclusive] <> [r].[MaxInclusive]) <> 1))
                        ;THROW 51005, 'CommissionRates migration aborted: active bands must be contiguous (no gaps or overlaps).', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM [CommissionRates]
                        WHERE [IsActive] = 1
                        GROUP BY [MinPrice]
                        HAVING COUNT(*) > 1)
                        ;THROW 51006, 'CommissionRates migration aborted: two active bands share the same lower bound.', 1;

                    IF EXISTS (SELECT 1 FROM [CommissionRates] WHERE [IsActive] = 1 AND ([RatePercent] <= 0 OR [RatePercent] > 100))
                        ;THROW 51007, 'CommissionRates migration aborted: an active commission rate is outside (0, 100].', 1;
                END;
                """);

            migrationBuilder.DropColumn(
                name: "MinInclusive",
                table: "CommissionRates");

            migrationBuilder.DropColumn(
                name: "MinPrice",
                table: "CommissionRates");

            // An open-ended band has no meaningful inclusivity; the domain forces it to false.
            migrationBuilder.Sql("UPDATE [CommissionRates] SET [MaxInclusive] = 0 WHERE [MaxPrice] IS NULL;");

            // Re-check the derived upper-bound topology. The lower bound now comes from the previous band, so the
            // only remaining invariants are the open-ended top and strictly increasing positive maxima.
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [CommissionRates])
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [CommissionRates] WHERE [IsActive] = 1)
                        ;THROW 51011, 'CommissionRates migration aborted: no active commission band exists.', 1;

                    IF (SELECT COUNT(*) FROM [CommissionRates] WHERE [IsActive] = 1 AND [MaxPrice] IS NULL) <> 1
                        ;THROW 51012, 'CommissionRates migration aborted: the active set must have exactly one open-ended top band.', 1;

                    IF EXISTS (SELECT 1 FROM [CommissionRates] WHERE [IsActive] = 1 AND [MaxPrice] IS NOT NULL AND [MaxPrice] <= 0)
                        ;THROW 51013, 'CommissionRates migration aborted: every bounded maximum must be greater than zero.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM [CommissionRates]
                        WHERE [IsActive] = 1 AND [MaxPrice] IS NOT NULL
                        GROUP BY [MaxPrice]
                        HAVING COUNT(*) > 1)
                        ;THROW 51014, 'CommissionRates migration aborted: bounded maxima must be unique.', 1;

                    IF EXISTS (SELECT 1 FROM [CommissionRates] WHERE [IsActive] = 1 AND ([RatePercent] <= 0 OR [RatePercent] > 100))
                        ;THROW 51015, 'CommissionRates migration aborted: an active commission rate is outside (0, 100].', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MinInclusive",
                table: "CommissionRates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MinPrice",
                table: "CommissionRates",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
