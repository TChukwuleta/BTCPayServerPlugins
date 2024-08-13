using System.Collections.Generic;

namespace BTCPayServer.Plugins.ShopifyPlugin.ViewModels.Models;

public class TransactionsListResp
{
    public List<TransactionDataHolder> transactions { get; set; }
}
