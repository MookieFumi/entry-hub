using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntryHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "room_blocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_blocks", x => x.Id);
                    table.CheckConstraint("ck_room_blocks_end_after_start", "\"EndAt\" > \"StartAt\"");
                    table.ForeignKey(
                        name: "FK_room_blocks_rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_room_blocks_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_room_blocks_RoomId_StartAt_EndAt",
                table: "room_blocks",
                columns: new[] { "RoomId", "StartAt", "EndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_room_blocks_TenantId",
                table: "room_blocks",
                column: "TenantId");

            // Same second barrier as the other tenant tables, see AddRowLevelSecurity.
            migrationBuilder.Sql("""
                ALTER TABLE room_blocks ENABLE ROW LEVEL SECURITY;
                ALTER TABLE room_blocks FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON room_blocks
                    USING ("TenantId" = nullif(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK ("TenantId" = nullif(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "room_blocks");
        }
    }
}
