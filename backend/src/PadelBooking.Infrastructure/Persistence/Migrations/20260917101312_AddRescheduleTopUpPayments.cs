using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRescheduleTopUpPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReservationId_Temporary",
                table: "Payments",
                column: "ReservationId");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReservationId",
                table: "Payments");

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "Payments",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "InitialBooking");

            migrationBuilder.AddColumn<DateTime>(
                name: "TargetEndTime",
                table: "Payments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TargetStartTime",
                table: "Payments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetTotalPrice",
                table: "Payments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Purpose_Status_TargetStartTime_TargetEndTime",
                table: "Payments",
                columns: new[] { "Purpose", "Status", "TargetStartTime", "TargetEndTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReservationId",
                table: "Payments",
                column: "ReservationId");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReservationId_Temporary",
                table: "Payments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReservationId_Temporary",
                table: "Payments",
                column: "ReservationId");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Purpose_Status_TargetStartTime_TargetEndTime",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReservationId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TargetEndTime",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TargetStartTime",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TargetTotalPrice",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReservationId",
                table: "Payments",
                column: "ReservationId",
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReservationId_Temporary",
                table: "Payments");
        }
    }
}
