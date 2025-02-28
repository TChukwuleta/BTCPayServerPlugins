using System;

namespace BTCPayServer.Plugins.SimpleTicketSales
{
    public class SimpleTicketSalesApiException : Exception
    {
        public SimpleTicketSalesApiException(string message) : base(message)
        {
        }
    }
}
