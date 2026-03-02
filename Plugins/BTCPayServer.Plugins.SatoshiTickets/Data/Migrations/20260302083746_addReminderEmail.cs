using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.SatoshiTickets.Data.Migrations
{
    /// <inheritdoc />
    public partial class addReminderEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReminderEmailBody",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "SatoshiTicketsSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReminderEmailSubject",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "SatoshiTicketsSettings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderEmailBody",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "SatoshiTicketsSettings");

            migrationBuilder.DropColumn(
                name: "ReminderEmailSubject",
                schema: "BTCPayServer.Plugins.SatoshiTickets",
                table: "SatoshiTicketsSettings");
        }
    }
}
