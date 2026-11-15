using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntryHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomBuffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BufferAfterMinutes",
                table: "rooms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BufferBeforeMinutes",
                table: "rooms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BlockedFrom",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BlockedUntil",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "BufferAfterMinutes",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BufferBeforeMinutes",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Existing bookings have no buffer, so the range they block is the one they already had. The table forces
            // row-level security, which would hide every row from this update, so it is lifted around it.
            migrationBuilder.Sql("""
                ALTER TABLE bookings DROP CONSTRAINT ex_bookings_room_no_overlap;
                ALTER TABLE bookings NO FORCE ROW LEVEL SECURITY;
                UPDATE bookings SET "BlockedFrom" = "StartAt", "BlockedUntil" = "EndAt";
                ALTER TABLE bookings FORCE ROW LEVEL SECURITY;
                ALTER TABLE bookings
                    ADD CONSTRAINT ex_bookings_room_no_overlap
                    EXCLUDE USING gist ("RoomId" WITH =, tstzrange("BlockedFrom", "BlockedUntil") WITH &&)
                    WHERE ("Status" = 'Confirmed');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE bookings DROP CONSTRAINT ex_bookings_room_no_overlap;
                ALTER TABLE bookings
                    ADD CONSTRAINT ex_bookings_room_no_overlap
                    EXCLUDE USING gist ("RoomId" WITH =, tstzrange("StartAt", "EndAt") WITH &&)
                    WHERE ("Status" = 'Confirmed');
                """);

            migrationBuilder.DropColumn(
                name: "BufferAfterMinutes",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "BufferBeforeMinutes",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "BlockedFrom",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "BlockedUntil",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "BufferAfterMinutes",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "BufferBeforeMinutes",
                table: "bookings");
        }
    }
}
