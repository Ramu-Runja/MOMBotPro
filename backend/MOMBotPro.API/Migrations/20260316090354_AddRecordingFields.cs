using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MOMBotPro.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecordingVideoUrl",
                table: "Pipelines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordingAudioUrl",
                table: "Pipelines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecordingExpiresAt",
                table: "Pipelines",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordingTranscriptJson",
                table: "Pipelines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BotId",
                table: "Pipelines",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RecordingVideoUrl",       table: "Pipelines");
            migrationBuilder.DropColumn(name: "RecordingAudioUrl",       table: "Pipelines");
            migrationBuilder.DropColumn(name: "RecordingExpiresAt",      table: "Pipelines");
            migrationBuilder.DropColumn(name: "RecordingTranscriptJson", table: "Pipelines");
            migrationBuilder.DropColumn(name: "BotId",                   table: "Pipelines");
        }
    }
}
