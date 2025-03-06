using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.SimpleTicketSales.Data.Migrations
{
    /// <inheritdoc />
    public partial class pluginSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "BTCPayServer.Plugins.SimpleTicketSale");

            migrationBuilder.CreateTable(
                name: "TicketSalesEvents",
                schema: "BTCPayServer.Plugins.SimpleTicketSale",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    EventImageUrl = table.Column<string>(type: "text", nullable: true),
                    EventLink = table.Column<string>(type: "text", nullable: true),
                    EventDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: true),
                    EmailSubject = table.Column<string>(type: "text", nullable: true),
                    EmailBody = table.Column<string>(type: "text", nullable: true),
                    HasMaximumCapacity = table.Column<bool>(type: "boolean", nullable: false),
                    MaximumEventCapacity = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSalesEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketSalesEventTickets",
                schema: "BTCPayServer.Plugins.SimpleTicketSale",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    EventId = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    InvoiceId = table.Column<string>(type: "text", nullable: true),
                    PaymentStatus = table.Column<string>(type: "text", nullable: true),
                    InvoiceStatus = table.Column<string>(type: "text", nullable: true),
                    EmailSent = table.Column<bool>(type: "boolean", nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketSalesEventTickets", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketSalesEvents",
                schema: "BTCPayServer.Plugins.SimpleTicketSale");

            migrationBuilder.DropTable(
                name: "TicketSalesEventTickets",
                schema: "BTCPayServer.Plugins.SimpleTicketSale");
        }
    }
}
