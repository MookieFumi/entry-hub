using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntryHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresApproval",
                table: "rooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // A pending booking holds its room like a confirmed one, so the constraint covers both.
            migrationBuilder.Sql("""
                ALTER TABLE bookings DROP CONSTRAINT ex_bookings_room_no_overlap;
                ALTER TABLE bookings
                    ADD CONSTRAINT ex_bookings_room_no_overlap
                    EXCLUDE USING gist ("RoomId" WITH =, tstzrange("BlockedFrom", "BlockedUntil") WITH &&)
                    WHERE ("Status" IN ('Confirmed', 'Pending'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE bookings NO FORCE ROW LEVEL SECURITY;
                UPDATE bookings SET "Status" = 'Cancelled', "CancelledAt" = now() WHERE "Status" = 'Pending';
                ALTER TABLE bookings FORCE ROW LEVEL SECURITY;
                ALTER TABLE bookings DROP CONSTRAINT ex_bookings_room_no_overlap;
                ALTER TABLE bookings
                    ADD CONSTRAINT ex_bookings_room_no_overlap
                    EXCLUDE USING gist ("RoomId" WITH =, tstzrange("BlockedFrom", "BlockedUntil") WITH &&)
                    WHERE ("Status" = 'Confirmed');
                """);

            migrationBuilder.DropColumn(
                name: "RequiresApproval",
                table: "rooms");
        }
    }
}
