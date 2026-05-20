using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSeedUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM auth_sessions
                WHERE "UserId" IN (
                    SELECT "Id"
                    FROM users
                    WHERE "Username" IN ('ivan.petrov', 'maria.sokolova', 'anna.admin', 'pavel.mod')
                );
                """);

            migrationBuilder.Sql("""
                DELETE FROM login_audit
                WHERE "UserId" IN (
                    SELECT "Id"
                    FROM users
                    WHERE "Username" IN ('ivan.petrov', 'maria.sokolova', 'anna.admin', 'pavel.mod')
                );
                """);

            migrationBuilder.Sql("""
                DELETE FROM users
                WHERE "Username" IN ('ivan.petrov', 'maria.sokolova', 'anna.admin', 'pavel.mod');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
