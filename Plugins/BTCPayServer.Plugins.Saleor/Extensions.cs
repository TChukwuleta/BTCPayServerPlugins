using System.Globalization;
using System.Linq;
using BTCPayServer.Services.Invoices;

namespace BTCPayServer.Plugins.Saleor;

public static class Extensions
{
    public const string SALEOR_ORDER_ID_PREFIX = "saleor-";
    public static string GetSaleorOrderId(this InvoiceEntity e) => e.GetInternalTags(SALEOR_ORDER_ID_PREFIX).FirstOrDefault();
}
