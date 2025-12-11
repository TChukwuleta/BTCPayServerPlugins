namespace BTCPayServer.Plugins.NairaCheckout.ViewModels;

public class NGNNameEquiry
{
    public string accountName { get; set; }
    public string accountNumber { get; set; }
    public string kycLevel { get; set; }
    public string nameInquiryReference { get; set; }
    public string channelCode { get; set; }
}


public class KESNameEnquiry
{
    public string status { get; set; }
    public KESNameEnquiryDataResponse data { get; set; }
}

public class KESNameEnquiryDataResponse
{
    public string organization_name { get; set; }
}