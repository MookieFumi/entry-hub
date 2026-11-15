using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntryHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSitesAndTenantRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rooms_TenantId_Name",
                table: "rooms");

            migrationBuilder.AddColumn<Guid>(
                name: "SiteId",
                table: "rooms",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "sites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Opens = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Closes = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    OpenDays = table.Column<int[]>(type: "integer[]", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sites_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_rules",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MinDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    SlotMinutes = table.Column<int>(type: "integer", nullable: false),
                    MinLeadTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxAdvanceDays = table.Column<int>(type: "integer", nullable: false),
                    CancellationNoticeMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxActiveBookingsPerEmployee = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_rules", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_tenant_rules_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Every tenant gets one site with the office hours the API had before sites existed (Monday to Friday,
            // 08:00 to 20:00, Madrid), and its rooms move there. The policy on rooms forces row security even for the
            // owner, which would hide every room from this statement, so it is lifted for the update only.
            migrationBuilder.Sql("""
                INSERT INTO sites ("Id", "TenantId", "Name", "TimeZoneId", "Opens", "Closes", "OpenDays", "IsActive", "CreatedAt")
                SELECT gen_random_uuid(), "Id", 'Main office', 'Europe/Madrid', '08:00', '20:00', ARRAY[1, 2, 3, 4, 5], true, now()
                FROM tenants;

                ALTER TABLE rooms NO FORCE ROW LEVEL SECURITY;
                UPDATE rooms SET "SiteId" = sites."Id" FROM sites WHERE sites."TenantId" = rooms."TenantId";
                ALTER TABLE rooms FORCE ROW LEVEL SECURITY;

                ALTER TABLE rooms ALTER COLUMN "SiteId" DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_rooms_SiteId_Name",
                table: "rooms",
                columns: new[] { "SiteId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sites_TenantId_Name",
                table: "sites",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_rooms_sites_SiteId",
                table: "rooms",
                column: "SiteId",
                principalTable: "sites",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Same second barrier as rooms and bookings, see AddRowLevelSecurity.
            foreach (var table in new[] { "sites", "tenant_rules" })
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {table} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON {table}
                        USING ("TenantId" = nullif(current_setting('app.tenant_id', true), '')::uuid)
                        WITH CHECK ("TenantId" = nullif(current_setting('app.tenant_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rooms_sites_SiteId",
                table: "rooms");

            migrationBuilder.DropTable(
                name: "sites");

            migrationBuilder.DropTable(
                name: "tenant_rules");

            migrationBuilder.DropIndex(
                name: "IX_rooms_SiteId_Name",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "rooms");

            migrationBuilder.CreateIndex(
                name: "IX_rooms_TenantId_Name",
                table: "rooms",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }
    }
}
