namespace BTCPayServer.Plugins.SatoshiTickets.ViewModels;

public class TicketScannerViewModel : BaseSimpleTicketPublicViewModel
{
    public bool PinRequired { get; set; }
    public string Token { get; set; }
    public string EventId { get; set; }
    public string EventName { get; set; }
}
