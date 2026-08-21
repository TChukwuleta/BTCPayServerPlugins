using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.SatoshiTickets.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddingReferrerDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KickbackType",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "DiscountCodes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "KickbackValue",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "DiscountCodes",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferrerId",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "DiscountCodes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReferralCredits",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    ReferrerId = table.Column<string>(type: "text", nullable: true),
                    EventId = table.Column<string>(type: "text", nullable: true),
                    OrderId = table.Column<string>(type: "text", nullable: true),
                    DiscountCodeId = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PayoutId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralCredits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReferralPayouts",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    ReferrerId = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralPayouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReferrerInvitations",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ReferrerId = table.Column<string>(type: "text", nullable: true),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Token = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferrerInvitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Referrers",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Referrers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferralCredits_OrderId",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "ReferralCredits",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralCredits_ReferrerId_Status",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "ReferralCredits",
                columns: new[] { "ReferrerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferrerInvitations_Token",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "ReferrerInvitations",
                column: "Token");

            migrationBuilder.CreateIndex(
                name: "IX_Referrers_StoreId_Email",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "Referrers",
                columns: new[] { "StoreId", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferralCredits",
                schema: "BTCPayServer.Plugins.SatoshiTickets");

            migrationBuilder.DropTable(
                name: "ReferralPayouts",
                schema: "BTCPayServer.Plugins.SatoshiTickets");

            migrationBuilder.DropTable(
                name: "ReferrerInvitations",
                schema: "BTCPayServer.Plugins.SatoshiTickets");

            migrationBuilder.DropTable(
                name: "Referrers",
                schema: "BTCPayServer.Plugins.SatoshiTickets");

            migrationBuilder.DropColumn(
                name: "KickbackType",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "DiscountCodes");

            migrationBuilder.DropColumn(
                name: "KickbackValue",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "DiscountCodes");

            migrationBuilder.DropColumn(
                name: "ReferrerId",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "DiscountCodes");
        }
    }
}
