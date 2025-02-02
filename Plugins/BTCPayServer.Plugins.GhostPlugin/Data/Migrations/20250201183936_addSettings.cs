using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.GhostPlugin.Data.Migrations
{
    /// <inheritdoc />
    public partial class addSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Setting",
                schema: "BTCPayServer.Plugins.Ghost",
                table: "GhostSettings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Setting",
                schema: "BTCPayServer.Plugins.Ghost",
                table: "GhostSettings");
        }
    }
}
