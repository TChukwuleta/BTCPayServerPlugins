using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.ShopifyPlugin.Data.Migrations
{
    /// <inheritdoc />
    public partial class includeOrderTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckoutToken",
                schema: "BTCPayServer.Plugins.Shopify",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiSecret",
                schema: "BTCPayServer.Plugins.Shopify",
                table: "ShopifySettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookId",
                schema: "BTCPayServer.Plugins.Shopify",
                table: "ShopifySettings",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "BTCPayServer.Plugins.Shopify",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ShopName = table.Column<string>(type: "text", nullable: true),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    OrderId = table.Column<string>(type: "text", nullable: true),
                    FinancialStatus = table.Column<string>(type: "text", nullable: true),
                    CheckoutId = table.Column<string>(type: "text", nullable: true),
                    CheckoutToken = table.Column<string>(type: "text", nullable: true),
                    OrderNumber = table.Column<string>(type: "text", nullable: true),
                    FulfilmentStatus = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Orders",
                schema: "BTCPayServer.Plugins.Shopify");

            migrationBuilder.DropColumn(
                name: "CheckoutToken",
                schema: "BTCPayServer.Plugins.Shopify",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ApiSecret",
                schema: "BTCPayServer.Plugins.Shopify",
                table: "ShopifySettings");

            migrationBuilder.DropColumn(
                name: "WebhookId",
                schema: "BTCPayServer.Plugins.Shopify",
                table: "ShopifySettings");
        }
    }
}
