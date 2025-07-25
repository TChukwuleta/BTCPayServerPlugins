using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.Salesforce.Data.Migrations
{
    /// <inheritdoc />
    public partial class includePaymentGatewayProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentGatewayProvider",
                schema: "BTCPayServer.Plugins.Salesforce",
                table: "SalesforceSettings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentGatewayProvider",
                schema: "BTCPayServer.Plugins.Salesforce",
                table: "SalesforceSettings");
        }
    }
}
