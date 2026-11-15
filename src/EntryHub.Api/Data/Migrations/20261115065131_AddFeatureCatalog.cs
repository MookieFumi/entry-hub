using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntryHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "features",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_features", x => x.Id);
                    table.ForeignKey(
                        name: "FK_features_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "room_features",
                columns: table => new
                {
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_features", x => new { x.RoomId, x.FeatureId });
                    table.ForeignKey(
                        name: "FK_room_features_features_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "features",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_room_features_rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // The free-text equipment of every room becomes catalog entries of its tenant. The two accessibility
            // entries get that kind and every other name is equipment. A code is the name in lowercase with hyphens;
            // names that only differ in case or punctuation share one entry. Row security on rooms is forced even for
            // the owner and would hide every room from this statement, so it is lifted for it.
            migrationBuilder.Sql("""
                ALTER TABLE rooms NO FORCE ROW LEVEL SECURITY;

                INSERT INTO features ("Id", "TenantId", "Code", "Name", "Kind", "IsActive", "CreatedAt")
                SELECT gen_random_uuid(), "TenantId", code, min(name),
                       CASE WHEN bool_or(lower(name) IN ('wheelchair access', 'induction loop')) THEN 'Accessibility' ELSE 'Equipment' END,
                       true, now()
                FROM (
                    SELECT r."TenantId", trim(item) AS name,
                           trim(both '-' from regexp_replace(lower(trim(item)), '[^a-z0-9]+', '-', 'g')) AS code
                    FROM rooms r CROSS JOIN LATERAL unnest(r."Equipment") AS item
                ) AS used
                WHERE code <> ''
                GROUP BY "TenantId", code;

                INSERT INTO room_features ("RoomId", "FeatureId", "TenantId")
                SELECT DISTINCT r."Id", f."Id", r."TenantId"
                FROM rooms r CROSS JOIN LATERAL unnest(r."Equipment") AS item
                JOIN features f ON f."TenantId" = r."TenantId"
                    AND f."Code" = trim(both '-' from regexp_replace(lower(trim(item)), '[^a-z0-9]+', '-', 'g'));

                ALTER TABLE rooms FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropColumn(
                name: "Equipment",
                table: "rooms");

            migrationBuilder.CreateIndex(
                name: "IX_features_TenantId_Code",
                table: "features",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_features_TenantId_Name",
                table: "features",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_room_features_FeatureId",
                table: "room_features",
                column: "FeatureId");

            // Same second barrier as rooms and bookings, see AddRowLevelSecurity.
            foreach (var table in new[] { "features", "room_features" })
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
            migrationBuilder.AddColumn<List<string>>(
                name: "Equipment",
                table: "rooms",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.Sql("""
                ALTER TABLE rooms NO FORCE ROW LEVEL SECURITY;
                UPDATE rooms SET "Equipment" = COALESCE(
                    (SELECT array_agg(f."Name" ORDER BY f."Name") FROM room_features rf JOIN features f ON f."Id" = rf."FeatureId" WHERE rf."RoomId" = rooms."Id"),
                    '{}');
                ALTER TABLE rooms FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "room_features");

            migrationBuilder.DropTable(
                name: "features");
        }
    }
}
