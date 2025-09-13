using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.NairaCheckout.Migrations
{
    /// <inheritdoc />
    public partial class includeStoreIdToCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalReference",
                table: "PayoutTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreId",
                table: "PayoutTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThirdPartyStatus",
                table: "PayoutTransactions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalReference",
                table: "PayoutTransactions");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "PayoutTransactions");

            migrationBuilder.DropColumn(
                name: "ThirdPartyStatus",
                table: "PayoutTransactions");
        }
    }
}
