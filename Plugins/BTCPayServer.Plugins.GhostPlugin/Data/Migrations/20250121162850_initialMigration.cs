using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.GhostPlugin.Data.Migrations
{
    /// <inheritdoc />
    public partial class initialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "BTCPayServer.Plugins.Ghost");

            migrationBuilder.CreateTable(
                name: "GhostMembers",
                schema: "BTCPayServer.Plugins.Ghost",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    MemberId = table.Column<string>(type: "text", nullable: true),
                    MemberUuid = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    SubscriptionId = table.Column<string>(type: "text", nullable: true),
                    TierId = table.Column<string>(type: "text", nullable: true),
                    UnsubscribeUrl = table.Column<string>(type: "text", nullable: true),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GhostMembers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GhostSettings",
                schema: "BTCPayServer.Plugins.Ghost",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ShopName = table.Column<string>(type: "text", nullable: true),
                    AdminDomain = table.Column<string>(type: "text", nullable: true),
                    AdminApiKey = table.Column<string>(type: "text", nullable: true),
                    ContentApiKey = table.Column<string>(type: "text", nullable: true),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    StoreName = table.Column<string>(type: "text", nullable: true),
                    ApplicationUserId = table.Column<string>(type: "text", nullable: true),
                    IntegratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GhostSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GhostMembers",
                schema: "BTCPayServer.Plugins.Ghost");

            migrationBuilder.DropTable(
                name: "GhostSettings",
                schema: "BTCPayServer.Plugins.Ghost");
        }
    }
}
