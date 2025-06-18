using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.NairaCheckout.Migrations
{
    /// <inheritdoc />
    public partial class initialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MavapaySettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApiKey = table.Column<string>(type: "text", nullable: true),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    WebhookSecret = table.Column<string>(type: "text", nullable: true),
                    StoreName = table.Column<string>(type: "text", nullable: true),
                    ApplicationUserId = table.Column<string>(type: "text", nullable: true),
                    IntegratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MavapaySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NairaCheckoutOrders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<string>(type: "text", nullable: true),
                    InvoiceId = table.Column<string>(type: "text", nullable: true),
                    ExternalReference = table.Column<string>(type: "text", nullable: true),
                    ExternalHash = table.Column<string>(type: "text", nullable: true),
                    InvoiceStatus = table.Column<string>(type: "text", nullable: true),
                    BTCPayMarkedPaid = table.Column<bool>(type: "boolean", nullable: false),
                    ThirdPartyMarkedPaid = table.Column<bool>(type: "boolean", nullable: false),
                    ThirdPartyStatus = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NairaCheckoutOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NairaCheckoutSettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    WalletName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NairaCheckoutSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MavapaySettings");

            migrationBuilder.DropTable(
                name: "NairaCheckoutOrders");

            migrationBuilder.DropTable(
                name: "NairaCheckoutSettings");
        }
    }
}
