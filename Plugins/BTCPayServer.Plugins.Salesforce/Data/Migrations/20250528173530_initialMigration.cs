using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.Salesforce.Data.Migrations
{
    /// <inheritdoc />
    public partial class initialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "BTCPayServer.Plugins.Salesforce");

            migrationBuilder.CreateTable(
                name: "SalesforceSettings",
                schema: "BTCPayServer.Plugins.Salesforce",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ConsumerKey = table.Column<string>(type: "text", nullable: true),
                    ConsumerSecret = table.Column<string>(type: "text", nullable: true),
                    SecurityToken = table.Column<string>(type: "text", nullable: true),
                    Username = table.Column<string>(type: "text", nullable: true),
                    Password = table.Column<string>(type: "text", nullable: true),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    StoreName = table.Column<string>(type: "text", nullable: true),
                    ApplicationUserId = table.Column<string>(type: "text", nullable: true),
                    IntegratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesforceSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesforceSettings",
                schema: "BTCPayServer.Plugins.Salesforce");
        }
    }
}
