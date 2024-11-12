using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.ShopifyPlugin.Data.Migrations
{
    /// <inheritdoc />
    public partial class updatePaymentMethodDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentText",
                schema: "BTCPayServer.Plugins.Shopify",
                table: "ShopifySettings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentText",
                schema: "BTCPayServer.Plugins.Shopify",
                table: "ShopifySettings");
        }
    }
}
