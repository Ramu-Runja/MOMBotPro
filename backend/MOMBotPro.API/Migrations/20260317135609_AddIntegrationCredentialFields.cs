using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MOMBotPro.API.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationCredentialFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessToken",
                table: "Integrations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Domain",
                table: "Integrations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Integrations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Owner",
                table: "Integrations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectKey",
                table: "Integrations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Repo",
                table: "Integrations",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AccessToken", table: "Integrations");
            migrationBuilder.DropColumn(name: "Domain",      table: "Integrations");
            migrationBuilder.DropColumn(name: "Email",       table: "Integrations");
            migrationBuilder.DropColumn(name: "Owner",       table: "Integrations");
            migrationBuilder.DropColumn(name: "ProjectKey",  table: "Integrations");
            migrationBuilder.DropColumn(name: "Repo",        table: "Integrations");
        }
    }
}
