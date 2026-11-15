using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntryHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingCreatedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "bookings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "bookings");
        }
    }
}
