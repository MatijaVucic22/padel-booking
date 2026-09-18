using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentFulfillmentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FulfillmentStatus",
                table: "Payments",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "ResolutionReasonCode",
                table: "Payments",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql("UPDATE `Payments` SET `FulfillmentStatus` = 'Applied' WHERE `Status` = 'Paid'");
            migrationBuilder.Sql("UPDATE `Payments` SET `FulfillmentStatus` = 'NotApplicable' WHERE `Status` IN ('Failed', 'Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FulfillmentStatus",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ResolutionReasonCode",
                table: "Payments");
        }
    }
}
