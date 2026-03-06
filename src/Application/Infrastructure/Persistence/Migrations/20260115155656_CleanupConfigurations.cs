using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CleanupConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert string Role values to integers before changing column type
            // Owner = 1, Member = 2
            migrationBuilder.Sql("""
                ALTER TABLE "UserOrganizations"
                ALTER COLUMN "Role" TYPE integer
                USING CASE "Role"
                    WHEN 'Owner' THEN 1
                    WHEN 'Member' THEN 2
                    ELSE 2
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Convert integer Role values back to strings
            // Owner = 1, Member = 2
            migrationBuilder.Sql("""
                ALTER TABLE "UserOrganizations"
                ALTER COLUMN "Role" TYPE character varying(50)
                USING CASE "Role"
                    WHEN 1 THEN 'Owner'
                    WHEN 2 THEN 'Member'
                    ELSE 'Member'
                END;
                """);
        }
    }
}
