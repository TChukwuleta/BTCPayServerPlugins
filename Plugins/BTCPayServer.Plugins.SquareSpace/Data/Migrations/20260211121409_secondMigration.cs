using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.SquareSpace.Data.Migrations
{
    /// <inheritdoc />
    public partial class secondMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CartData",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CartId",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CartToken",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerEmail",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Items",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidAt",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amount",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders");

            migrationBuilder.DropColumn(
                name: "CartData",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders");

            migrationBuilder.DropColumn(
                name: "CartId",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders");

            migrationBuilder.DropColumn(
                name: "CartToken",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders");

            migrationBuilder.DropColumn(
                name: "CustomerEmail",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders");

            migrationBuilder.DropColumn(
                name: "Items",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                schema: "BTCPayServer.Plugins.SquareSpace",
                table: "SquareSpaceOrders");
        }
    }
}
