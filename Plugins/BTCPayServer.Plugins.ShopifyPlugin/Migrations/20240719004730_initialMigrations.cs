using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.BigCommercePlugin.Migrations
{
    /// <inheritdoc />
    public partial class initialMigrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "BTCPayServer.Plugins.BigCommerce");

            migrationBuilder.CreateTable(
                name: "BigCommerceStores",
                schema: "BTCPayServer.Plugins.BigCommerce",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ClientId = table.Column<string>(type: "text", nullable: true),
                    ClientSecret = table.Column<string>(type: "text", nullable: true),
                    RedirectUrl = table.Column<string>(type: "text", nullable: true),
                    AccessToken = table.Column<string>(type: "text", nullable: true),
                    Scope = table.Column<string>(type: "text", nullable: true),
                    StoreHash = table.Column<string>(type: "text", nullable: true),
                    BigCommerceUserId = table.Column<string>(type: "text", nullable: true),
                    BigCommerceUserEmail = table.Column<string>(type: "text", nullable: true),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    StoreName = table.Column<string>(type: "text", nullable: true),
                    ApplicationUserId = table.Column<string>(type: "text", nullable: true),
                    JsFileUuid = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BigCommerceStores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                schema: "BTCPayServer.Plugins.BigCommerce",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ClientId = table.Column<string>(type: "text", nullable: true),
                    StoreHash = table.Column<string>(type: "text", nullable: true),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    OrderId = table.Column<string>(type: "text", nullable: true),
                    InvoiceId = table.Column<string>(type: "text", nullable: true),
                    InvoiceStatus = table.Column<int>(type: "integer", nullable: false),
                    TransactionStatus = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BigCommerceStores",
                schema: "BTCPayServer.Plugins.BigCommerce");

            migrationBuilder.DropTable(
                name: "Transactions",
                schema: "BTCPayServer.Plugins.BigCommerce");
        }
    }
}
