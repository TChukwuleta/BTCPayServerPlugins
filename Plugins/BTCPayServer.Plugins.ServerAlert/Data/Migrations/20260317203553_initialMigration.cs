using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.ServerAlert.Data.Migrations
{
    /// <inheritdoc />
    public partial class initialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "BTCPayServer.Plugins.ServerAlert");

            migrationBuilder.CreateTable(
                name: "Announcements",
                schema: "BTCPayServer.Plugins.ServerAlert",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    BellNotificationsSent = table.Column<bool>(type: "boolean", nullable: false),
                    EmailScope = table.Column<int>(type: "integer", nullable: false),
                    SelectedStoreIds = table.Column<string>(type: "text", nullable: true),
                    CustomEmailAddresses = table.Column<string>(type: "text", nullable: true),
                    EmailsSent = table.Column<bool>(type: "boolean", nullable: false),
                    EmailsSentCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_IsPublished_CreatedAt",
                schema: "BTCPayServer.Plugins.ServerAlert",
                table: "Announcements",
                columns: new[] { "IsPublished", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Announcements",
                schema: "BTCPayServer.Plugins.ServerAlert");
        }
    }
}
