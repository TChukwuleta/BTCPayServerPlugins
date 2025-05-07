using System;

namespace BTCPayServer.Plugins.Salesforce
{
    public class SalesforceApiException : Exception
    {
        public SalesforceApiException(string message) : base(message)
        {
        }
    }
}
