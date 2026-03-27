using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MOMBotPro.API.Migrations
{
    /// <inheritdoc />
    public partial class AddExpiresAtToIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "Integrations",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Integrations");
        }
    }
}
