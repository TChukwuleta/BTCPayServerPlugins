using System;
using System.Collections.Generic;
using Microsoft.AspNet.SignalR.Client.Hubs;

namespace BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;

public class CreateMemberResponseModel
{
    public List<MemberCreationResponse> members { get; set; }
}
public class MemberCreationResponse
{
    public string id { get; set; }
    public string uuid { get; set; }
    public string email { get; set; }
    public string name { get; set; }
    public object note { get; set; }
    public object geolocation { get; set; }
    public bool subscribed { get; set; }
    public object created_at { get; set; }
    public object updated_at { get; set; }
    public List<object> labels { get; set; }
    public List<object> subscriptions { get; set; }
    public string avatar_image { get; set; }
    public bool comped { get; set; }
    public int email_count { get; set; }
    public int email_opened_count { get; set; }
    public object email_open_rate { get; set; }
    public string status { get; set; }
    public object last_seen_at { get; set; }
    public object attribution { get; set; }
    public string unsubscribe_url { get; set; }
    public List<object> tiers { get; set; }
    public object email_suppression { get; set; }
    public List<object> newsletters { get; set; }
}