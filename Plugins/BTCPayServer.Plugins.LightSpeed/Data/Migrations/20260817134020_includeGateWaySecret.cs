using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.LightSpeed.Data.Migrations
{
    /// <inheritdoc />
    public partial class includeGateWaySecret : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GatewaySecret",
                schema: "BTCPayServer.Plugins.LightspeedHQ",
                table: "LightspeedSettings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GatewaySecret",
                schema: "BTCPayServer.Plugins.LightspeedHQ",
                table: "LightspeedSettings");
        }
    }
}
