using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoEncoder.Migrations
{
    /// <inheritdoc />
    public partial class VideoName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VideoName",
                table: "qt_videos",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VideoName",
                table: "qt_videos");
        }
    }
}
