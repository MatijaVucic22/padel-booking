using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CheckedInAtUtc",
                table: "Reservations",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckedInAtUtc",
                table: "Reservations");
        }
    }
}
