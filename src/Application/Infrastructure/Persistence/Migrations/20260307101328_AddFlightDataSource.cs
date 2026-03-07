using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightDataSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FlightDataSource",
                table: "AirportConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FlightDataSourceConfigJson",
                table: "AirportConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedAt",
                table: "AirportConfigs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlightDataSource",
                table: "AirportConfigs");

            migrationBuilder.DropColumn(
                name: "FlightDataSourceConfigJson",
                table: "AirportConfigs");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "AirportConfigs");
        }
    }
}
