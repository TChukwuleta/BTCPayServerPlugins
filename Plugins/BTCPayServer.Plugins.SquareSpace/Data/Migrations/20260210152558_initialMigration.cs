using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.SquareSpace.Data.Migrations
{
    /// <inheritdoc />
    public partial class initialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "BTCPayServer.Plugins.SquareSpace");

            migrationBuilder.CreateTable(
                name: "SquareSpaceOrders",
                schema: "BTCPayServer.Plugins.SquareSpace",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    SquarespaceOrderId = table.Column<string>(type: "text", nullable: true),
                    SquarespaceOrderNumber = table.Column<string>(type: "text", nullable: true),
                    InvoiceId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SquareSpaceOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SquareSpaceSettings",
                schema: "BTCPayServer.Plugins.SquareSpace",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OAuthToken = table.Column<string>(type: "text", nullable: true),
                    WebsiteId = table.Column<string>(type: "text", nullable: true),
                    WebhookEndpointUrl = table.Column<string>(type: "text", nullable: true),
                    WebhookSecret = table.Column<string>(type: "text", nullable: true),
                    WebhookSubscriptionId = table.Column<string>(type: "text", nullable: true),
                    AutoCreateInvoices = table.Column<bool>(type: "boolean", nullable: false),
                    StoreId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SquareSpaceSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
            name: "IX_SquarespaceOrders_StoreId",
            schema: "BTCPayServer.Plugins.SquareSpace",
            table: "SquareSpaceOrders",
            column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_SquarespaceOrders_SquarespaceOrderId",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders",
                column: "SquarespaceOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SquarespaceOrders_InvoiceId",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders",
                column: "InvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SquareSpaceOrders",
                schema: "BTCPayServer.Plugins.SquareSpace");

            migrationBuilder.DropTable(
                name: "SquareSpaceSettings",
                schema: "BTCPayServer.Plugins.SquareSpace");
        }
    }
}
