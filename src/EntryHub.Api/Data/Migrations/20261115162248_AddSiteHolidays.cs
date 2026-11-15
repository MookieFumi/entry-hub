using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntryHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteHolidays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "site_holidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_holidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_site_holidays_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_site_holidays_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_site_holidays_SiteId_Date",
                table: "site_holidays",
                columns: new[] { "SiteId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_site_holidays_TenantId",
                table: "site_holidays",
                column: "TenantId");

            // Same second barrier as the other tenant tables, see AddRowLevelSecurity.
            migrationBuilder.Sql("""
                ALTER TABLE site_holidays ENABLE ROW LEVEL SECURITY;
                ALTER TABLE site_holidays FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON site_holidays
                    USING ("TenantId" = nullif(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK ("TenantId" = nullif(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "site_holidays");
        }
    }
}
