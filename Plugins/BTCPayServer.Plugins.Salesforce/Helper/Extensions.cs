using static BTCPayServer.Plugins.Salesforce.Services.SalesforceApiClient;
using BTCPayServer.Plugins.Salesforce.Data;
using BTCPayServer.Services.Invoices;
using System.Linq;
using System.Globalization;

namespace BTCPayServer.Plugins.Salesforce.Helper;

public static class Extensions
{
    public const string StoreBlobKey = "salesforce";
    public const string SALESFORCE_ORDER_ID_PREFIX = "salesforce-";
    public static SalesforceApiClientCredentials CreateSalesforceApiCredentials(this SalesforceSetting salesforce)
    {
        return new SalesforceApiClientCredentials
        {
            ConsumerKey = salesforce.ConsumerKey,
            ConsumerSecret = salesforce.ConsumerSecret,
            Password = salesforce.Password,
            Username = salesforce.Username
        };
    }
    public static long? GetSalesforceOrderId(this InvoiceEntity e)
        => e
            .GetInternalTags(SALESFORCE_ORDER_ID_PREFIX)
            .Select(e => long.TryParse(e, CultureInfo.InvariantCulture, out var v) ? v : (long?)null)
            .FirstOrDefault(e => e is not null);
}
