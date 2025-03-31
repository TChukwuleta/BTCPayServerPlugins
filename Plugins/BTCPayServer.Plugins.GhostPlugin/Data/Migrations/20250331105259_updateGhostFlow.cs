using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.GhostPlugin.Data.Migrations
{
    /// <inheritdoc />
    public partial class updateGhostFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Password",
                schema: "BTCPayServer.Plugins.Ghost",
                table: "GhostSettings");

            migrationBuilder.DropColumn(
                name: "Username",
                schema: "BTCPayServer.Plugins.Ghost",
                table: "GhostSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Password",
                schema: "BTCPayServer.Plugins.Ghost",
                table: "GhostSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                schema: "BTCPayServer.Plugins.Ghost",
                table: "GhostSettings",
                type: "text",
                nullable: true);
        }
    }
}
