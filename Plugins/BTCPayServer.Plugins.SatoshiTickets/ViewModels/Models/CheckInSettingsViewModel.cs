namespace BTCPayServer.Plugins.SatoshiTickets.ViewModels;

public class CheckInSettingsViewModel
{
    public string StoreId { get; set; }
    public string EventId { get; set; }
    public string EventTitle { get; set; }
    public string CheckInUrl { get; set; }
    public bool PinEnabled { get; set; }
    public string Pin { get; set; }
    public bool HasExistingPin { get; set; }
}
