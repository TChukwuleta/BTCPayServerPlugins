using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.JumpSeller.Data.Migrations
{
    /// <inheritdoc />
    public partial class initialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "BTCPayServer.Plugins.JumpSeller");

            migrationBuilder.CreateTable(
                name: "JumpSellerInvoices",
                schema: "BTCPayServer.Plugins.JumpSeller",
                columns: table => new
                {
                    InvoiceId = table.Column<string>(type: "text", nullable: false),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    OrderReference = table.Column<string>(type: "text", nullable: true),
                    CallbackUrl = table.Column<string>(type: "text", nullable: true),
                    CompleteUrl = table.Column<string>(type: "text", nullable: true),
                    CancelUrl = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<string>(type: "text", nullable: true),
                    Currency = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CallbackSent = table.Column<bool>(type: "boolean", nullable: false),
                    LastResult = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JumpSellerInvoices", x => x.InvoiceId);
                });

            migrationBuilder.CreateTable(
                name: "JumpSellerStoreSettings",
                schema: "BTCPayServer.Plugins.JumpSeller",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    EpgAccountId = table.Column<string>(type: "text", nullable: true),
                    EpgSecret = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JumpSellerStoreSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JumpSellerInvoices",
                schema: "BTCPayServer.Plugins.JumpSeller");

            migrationBuilder.DropTable(
                name: "JumpSellerStoreSettings",
                schema: "BTCPayServer.Plugins.JumpSeller");
        }
    }
}
