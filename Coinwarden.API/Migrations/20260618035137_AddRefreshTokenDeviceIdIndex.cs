using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coinwarden.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenDeviceIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_DeviceId",
                table: "RefreshTokens",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_DeviceId",
                table: "RefreshTokens");
        }
    }
}
