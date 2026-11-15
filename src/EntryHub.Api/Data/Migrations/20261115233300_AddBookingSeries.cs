using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntryHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                table: "bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeriesPosition",
                table: "bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "booking_series",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Interval = table.Column<int>(type: "integer", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: true),
                    Until = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_series", x => x.Id);
                    table.ForeignKey(
                        name: "FK_booking_series_rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_booking_series_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_SeriesId",
                table: "bookings",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_booking_series_RoomId",
                table: "booking_series",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_booking_series_TenantId",
                table: "booking_series",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_booking_series_SeriesId",
                table: "bookings",
                column: "SeriesId",
                principalTable: "booking_series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Same second barrier as the other tenant tables, see AddRowLevelSecurity.
            migrationBuilder.Sql("""
                ALTER TABLE booking_series ENABLE ROW LEVEL SECURITY;
                ALTER TABLE booking_series FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON booking_series
                    USING ("TenantId" = nullif(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK ("TenantId" = nullif(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bookings_booking_series_SeriesId",
                table: "bookings");

            migrationBuilder.DropTable(
                name: "booking_series");

            migrationBuilder.DropIndex(
                name: "IX_bookings_SeriesId",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "SeriesPosition",
                table: "bookings");
        }
    }
}
