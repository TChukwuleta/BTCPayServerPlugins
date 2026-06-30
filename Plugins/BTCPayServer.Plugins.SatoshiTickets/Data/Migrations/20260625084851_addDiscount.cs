using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.SatoshiTickets.Data.Migrations
{
    /// <inheritdoc />
    public partial class addDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountCodeId",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountCodeValue",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalAmount",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "Orders",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiscountCodes",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    EventId = table.Column<string>(type: "text", nullable: true),
                    TicketTypeId = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: true),
                    DiscountType = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxUses = table.Column<int>(type: "integer", nullable: true),
                    UsesCount = table.Column<int>(type: "integer", nullable: false),
                    ExpiryDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MinQuantity = table.Column<int>(type: "integer", nullable: true),
                    DiscountCodeState = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountCodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscountCodes_EventId_Code",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "DiscountCodes",
                columns: new[] { "EventId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscountCodes",
                schema: "BTCPayServer.Plugins.SatoshiTickets");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountCodeId",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountCodeValue",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SubtotalAmount",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "Orders");
        }
    }
}
