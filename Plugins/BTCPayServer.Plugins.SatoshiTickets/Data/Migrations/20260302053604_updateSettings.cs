using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.SatoshiTickets.Data.Migrations
{
    /// <inheritdoc />
    public partial class updateSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReminderDaysBeforeEvent",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "Events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReminderEnabled",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "Events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReminderSentAt",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "Events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SatoshiTicketsSettings",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    StoreId = table.Column<string>(type: "text", nullable: true),
                    EnableAutoReminders = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultReminderDaysBeforeEvent = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatoshiTicketsSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SatoshiTicketsSettings",
                schema: "BTCPayServer.Plugins.SatoshiTickets");

            migrationBuilder.DropColumn(
                name: "ReminderDaysBeforeEvent",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ReminderEnabled",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "Events");
        }
    }
}
