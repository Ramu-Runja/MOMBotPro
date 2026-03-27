using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MOMBotPro.API.Migrations
{
    /// <inheritdoc />
    public partial class AddIsArchivedToPipelines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Pipelines",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Pipelines");
        }
    }
}
