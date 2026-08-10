using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.BigCommercePlugin.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionIndexx : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Transactions_OrderId",
                schema: "BTCPayServer.Plugins.BigCommerce",
                table: "Transactions",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_OrderId",
                schema: "BTCPayServer.Plugins.BigCommerce",
                table: "Transactions");
        }
    }
}
