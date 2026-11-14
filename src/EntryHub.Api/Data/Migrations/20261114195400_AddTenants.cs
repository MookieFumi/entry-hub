using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntryHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rooms_Floor_Capacity",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "IX_rooms_Name",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "IX_bookings_OrganizerId_Status_EndAt",
                table: "bookings");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "rooms",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "bookings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            // Rooms and bookings that exist before tenants do belong to a default tenant, so nothing is lost.
            migrationBuilder.InsertData(
                table: "tenants",
                columns: new[] { "Id", "Name", "CreatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), "Default", new DateTimeOffset(2026, 11, 14, 0, 0, 0, TimeSpan.Zero) });

            // The default only back-fills existing rows. New rows must name their tenant.
            migrationBuilder.Sql("ALTER TABLE rooms ALTER COLUMN \"TenantId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE bookings ALTER COLUMN \"TenantId\" DROP DEFAULT;");

            migrationBuilder.CreateIndex(
                name: "IX_rooms_TenantId_Floor_Capacity",
                table: "rooms",
                columns: new[] { "TenantId", "Floor", "Capacity" });

            migrationBuilder.CreateIndex(
                name: "IX_rooms_TenantId_Name",
                table: "rooms",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_TenantId_OrganizerId_Status_EndAt",
                table: "bookings",
                columns: new[] { "TenantId", "OrganizerId", "Status", "EndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Name",
                table: "tenants",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_tenants_TenantId",
                table: "bookings",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_rooms_tenants_TenantId",
                table: "rooms",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bookings_tenants_TenantId",
                table: "bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_rooms_tenants_TenantId",
                table: "rooms");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_rooms_TenantId_Floor_Capacity",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "IX_rooms_TenantId_Name",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "IX_bookings_TenantId_OrganizerId_Status_EndAt",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "bookings");

            migrationBuilder.CreateIndex(
                name: "IX_rooms_Floor_Capacity",
                table: "rooms",
                columns: new[] { "Floor", "Capacity" });

            migrationBuilder.CreateIndex(
                name: "IX_rooms_Name",
                table: "rooms",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_OrganizerId_Status_EndAt",
                table: "bookings",
                columns: new[] { "OrganizerId", "Status", "EndAt" });
        }
    }
}
