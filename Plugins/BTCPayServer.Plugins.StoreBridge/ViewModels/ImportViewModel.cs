namespace BTCPayServer.Plugins.StoreBridge.ViewModels;

public class ImportViewModel
{
    public string StoreId { get; set; }
    public StoreImportOptions Options { get; set; } = new();
}
