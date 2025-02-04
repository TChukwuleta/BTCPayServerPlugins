using System;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.GhostPlugin.ViewModels;

public class GetMemberResponse
{
    public List<SingleMember> members { get; set; }
    public object meta { get; set; }
}

public class SingleMember
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
    public string unsubscribe_url { get; set; }
    public object email_suppression { get; set; }
    public List<Newsletter> newsletters { get; set; }
}


public class Newsletter
{
    public string id { get; set; }
    public string name { get; set; }
    public object description { get; set; }
    public string status { get; set; }
}