using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmEnabledToAirportConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LlmEnabled",
                table: "AirportConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LlmEnabled",
                table: "AirportConfigs");
        }
    }
}
