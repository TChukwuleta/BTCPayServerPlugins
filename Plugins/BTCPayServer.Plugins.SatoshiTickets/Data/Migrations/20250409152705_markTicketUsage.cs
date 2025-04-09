using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.SatoshiTickets.Data.Migrations
{
    /// <inheritdoc />
    public partial class markTicketUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UsedAt",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsedAt",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "Tickets");
        }
    }
}
