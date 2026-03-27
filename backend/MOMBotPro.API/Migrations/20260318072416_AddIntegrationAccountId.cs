using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MOMBotPro.API.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationAccountId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountId",
                table: "Integrations",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Integrations");
        }
    }
}
