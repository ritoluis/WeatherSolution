using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeatherApi.Migrations
{
    /// <inheritdoc />
    public partial class DeviceApiKeyRotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "PendingApiKeyEncrypted",
                table: "Devices",
                type: "longblob",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "PendingApiKeyIv",
                table: "Devices",
                type: "longblob",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingApiKeyEncrypted",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "PendingApiKeyIv",
                table: "Devices");
        }
    }
}
