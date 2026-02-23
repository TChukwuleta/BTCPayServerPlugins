using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.LightSpeed.Data.Migrations
{
    /// <inheritdoc />
    public partial class initialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "BTCPayServer.Plugins.LightspeedHQ");

            migrationBuilder.CreateTable(
                name: "LightSpeedPayments",
                schema: "BTCPayServer.Plugins.LightspeedHQ",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InvoiceId = table.Column<string>(type: "text", nullable: true),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    RegisterSaleId = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LightSpeedPayments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LightspeedSettings",
                schema: "BTCPayServer.Plugins.LightspeedHQ",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    LightspeedDomainPrefix = table.Column<string>(type: "text", nullable: true),
                    LightSpeedUrl = table.Column<string>(type: "text", nullable: true),
                    LightspeedPersonalAccessToken = table.Column<string>(type: "text", nullable: true),
                    Currency = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LightspeedSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LightSpeedPayments",
                schema: "BTCPayServer.Plugins.LightspeedHQ");

            migrationBuilder.DropTable(
                name: "LightspeedSettings",
                schema: "BTCPayServer.Plugins.LightspeedHQ");
        }
    }
}
