namespace BTCPayServer.Plugins.ServerAlert.ViewModels;

public class PublishResult
{
    public bool Found { get; init; }
    public bool EmailScopeWasSet { get; init; }
    public int BellCount { get; set; }
    public int EmailCount { get; set; }
}