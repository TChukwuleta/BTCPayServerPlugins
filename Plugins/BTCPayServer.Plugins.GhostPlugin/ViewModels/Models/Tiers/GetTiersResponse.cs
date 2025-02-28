using System;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;

public class GetTiersResponse
{
    public List<Tier> tiers { get; set; }
    public object meta { get; set; }
}

public class Tier
{
    public string id { get; set; }
    public string name { get; set; }
    public object description { get; set; }
    public string slug { get; set; }
    public bool active { get; set; }
    public string type { get; set; }
    public object welcome_page_url { get; set; }
    public DateTime? created_at { get; set; }
    public object updated_at { get; set; }
    public string visibility { get; set; }
    public List<object> benefits { get; set; }
    public int trial_days { get; set; }
    public string currency { get; set; }
    public int? monthly_price { get; set; }
    public int? yearly_price { get; set; }
}
