using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PadelBooking.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCourtImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Courts",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Courts");
        }
    }
}
