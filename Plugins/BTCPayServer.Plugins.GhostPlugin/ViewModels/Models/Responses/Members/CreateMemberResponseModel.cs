using System;
using System.Collections.Generic;

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
    public object name { get; set; }
    public object note { get; set; }
    public object geolocation { get; set; }
    public bool subscribed { get; set; }
    public DateTime created_at { get; set; }
    public DateTime updated_at { get; set; }
    public List<object> labels { get; set; }
    public List<Subscription> subscriptions { get; set; }
    public string avatar_image { get; set; }
    public bool comped { get; set; }
    public int email_count { get; set; }
    public int email_opened_count { get; set; }
    public object email_open_rate { get; set; }
    public string status { get; set; }
    public object last_seen_at { get; set; }
    public object attribution { get; set; }
    public string unsubscribe_url { get; set; }
    public List<SubscriptionTierResponse> tiers { get; set; }
    public object email_suppression { get; set; }
    public List<object> newsletters { get; set; }
}

public class Subscription
{
    public string id { get; set; }
    public SubscriptionTierResponse tier { get; set; }
    public object customer { get; set; }
    public Plan plan { get; set; }
    public string status { get; set; }
    public DateTime start_date { get; set; }
    public string default_payment_card_last4 { get; set; }
    public bool cancel_at_period_end { get; set; }
    public object cancellation_reason { get; set; }
    public DateTime current_period_end { get; set; }
    public Price price { get; set; }
    public object offer { get; set; }
}


public class Plan
{
    public string id { get; set; }
    public string nickname { get; set; }
    public string interval { get; set; }
    public string currency { get; set; }
    public int amount { get; set; }
}

public class Price
{
    public string id { get; set; }
    public string price_id { get; set; }
    public string nickname { get; set; }
    public int amount { get; set; }
    public string interval { get; set; }
    public string type { get; set; }
    public string currency { get; set; }
    public object tier { get; set; }
}


public class SubscriptionTierResponse
{
    public string id { get; set; }
    public string name { get; set; }
    public string slug { get; set; }
    public bool active { get; set; }
    public object welcome_page_url { get; set; }
    public string visibility { get; set; }
    public int trial_days { get; set; }
    public object description { get; set; }
    public string type { get; set; }
    public string currency { get; set; }
    public int monthly_price { get; set; }
    public int yearly_price { get; set; }
    public DateTime created_at { get; set; }
    public DateTime updated_at { get; set; }
    public object monthly_price_id { get; set; }
    public object yearly_price_id { get; set; }
    public DateTime expiry_at { get; set; }
    public string tier_id { get; set; }
}

