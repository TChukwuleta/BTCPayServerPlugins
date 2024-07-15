using System;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.BigCommercePlugin.ViewModels;

public class CreateCheckoutScriptResponse
{
    public CheckoutResponseData data { get; set; }
    public CheckoutMetaData meta { get; set; }
}

public class CheckoutResponseData
{
    public string name { get; set; }
    public string uuid { get; set; }
    public DateTime date_created { get; set; }
    public DateTime date_modified { get; set; }
    public string description { get; set; }
    public string html { get; set; }
    public string src { get; set; }
    public bool auto_uninstall { get; set; }
    public string load_method { get; set; }
    public string location { get; set; }
    public string visibility { get; set; }
    public string kind { get; set; }
    public string api_client_id { get; set; }
    public string consent_category { get; set; }
    public bool enabled { get; set; }
    public int channel_id { get; set; }
    public List<string> integrity_hashes { get; set; }
}

public class CheckoutMetaData
{
}
