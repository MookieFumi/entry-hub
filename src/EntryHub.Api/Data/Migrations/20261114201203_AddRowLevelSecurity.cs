using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntryHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Second barrier behind the tenant filter of EF Core: even a query that forgets the filter only sees the
            // rows of the tenant in app.tenant_id, which TenantConnectionInterceptor sets on every connection.
            // FORCE applies the policy to the table owner too. Superusers still bypass it, so the API must connect
            // with an ordinary role (see the setup guide).
            migrationBuilder.Sql("""
                ALTER TABLE rooms ENABLE ROW LEVEL SECURITY;
                ALTER TABLE rooms FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON rooms
                    USING ("TenantId" = nullif(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK ("TenantId" = nullif(current_setting('app.tenant_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE bookings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE bookings FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON bookings
                    USING ("TenantId" = nullif(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK ("TenantId" = nullif(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY tenant_isolation ON bookings;
                ALTER TABLE bookings NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE bookings DISABLE ROW LEVEL SECURITY;
                DROP POLICY tenant_isolation ON rooms;
                ALTER TABLE rooms NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE rooms DISABLE ROW LEVEL SECURITY;
                """);
        }
    }
}
