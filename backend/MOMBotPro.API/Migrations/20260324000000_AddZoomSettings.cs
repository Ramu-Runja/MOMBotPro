using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MOMBotPro.API.Migrations
{
    /// <inheritdoc />
    public partial class AddZoomSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZoomSettings",
                columns: table => new
                {
                    Id            = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId        = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ZoomLink      = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsRecurring   = table.Column<bool>(type: "bit", nullable: false),
                    ScheduledTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    ScheduledDays = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive      = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt     = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoomSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZoomSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZoomSettings_UserId",
                table: "ZoomSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ZoomSettings");
        }
    }
}
