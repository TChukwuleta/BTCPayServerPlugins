using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.BigCommercePlugin.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCartIdRToTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CartId",
                schema: "BTCPayServer.Plugins.BigCommerce",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_StoreId_CartId",
                schema: "BTCPayServer.Plugins.BigCommerce",
                table: "Transactions",
                columns: new[] { "StoreId", "CartId" },
                unique: true,
                filter: "\"CartId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_StoreId_CartId",
                schema: "BTCPayServer.Plugins.BigCommerce",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CartId",
                schema: "BTCPayServer.Plugins.BigCommerce",
                table: "Transactions");
        }
    }
}
