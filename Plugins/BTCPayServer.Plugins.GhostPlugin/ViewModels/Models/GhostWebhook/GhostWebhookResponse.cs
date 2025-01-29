using System.Collections.Generic;
using System;

namespace BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;

public class GhostWebhookResponse
{
    public WebhookMemberResponse member { get; set; }
}

public class WebhookMemberResponse
{
    public WebhookCurrentResponse current { get; set; }
    public Previous previous { get; set; }
}

public class WebhookCurrentResponse
{
    public string id { get; set; }
    public string uuid { get; set; }
    public string email { get; set; }
    public string name { get; set; }
    public object note { get; set; }
    public object geolocation { get; set; }
    public bool subscribed { get; set; }
    public DateTime created_at { get; set; }
    public DateTime updated_at { get; set; }
    public List<object> labels { get; set; }
    public List<object> subscriptions { get; set; }
    public string avatar_image { get; set; }
    public bool comped { get; set; }
    public int email_count { get; set; }
    public int email_opened_count { get; set; }
    public object email_open_rate { get; set; }
    public string status { get; set; }
    public object last_seen_at { get; set; }
    public List<object> tiers { get; set; }
    public List<object> newsletters { get; set; }
}

public class Previous
{
    public List<object> newsletters { get; set; }
    public string id { get; set; }
    public string uuid { get; set; }
    public string email { get; set; }
    public string status { get; set; }
    public string name { get; set; }
    public object note { get; set; }
    public string geolocation { get; set; }
    public int email_count { get; set; }
    public int email_opened_count { get; set; }
    public object email_open_rate { get; set; }
    public DateTime last_seen_at { get; set; }
    public DateTime created_at { get; set; }
    public DateTime updated_at { get; set; }
}
