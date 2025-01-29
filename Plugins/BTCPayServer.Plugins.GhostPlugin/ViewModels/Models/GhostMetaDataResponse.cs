namespace BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;

public class GhostMetaDataResponse
{
    public Pagination pagination { get; set; }
}

public class Pagination
{
    public int page { get; set; }
    public int pages { get; set; }
    public int limit { get; set; }
    public int total { get; set; }
    public object prev { get; set; }
    public object next { get; set; }
}

