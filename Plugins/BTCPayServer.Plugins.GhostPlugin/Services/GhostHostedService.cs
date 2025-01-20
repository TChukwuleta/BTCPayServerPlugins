using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Services.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace BTCPayServer.Plugins.GhostPlugin.Services;

public class GhostHostedService : EventHostedServiceBase
{
    private readonly ILogger<GhostHostedService> _clientLogger;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GhostDbContextFactory _dbContextFactory;

    public GhostHostedService(EventAggregator eventAggregator,
        InvoiceRepository invoiceRepository,
        IHttpClientFactory httpClientFactory,
        GhostDbContextFactory dbContextFactory,
        ILogger<GhostHostedService> clientLogger,
        Logs logs) : base(eventAggregator, logs)
    {
        _clientLogger = clientLogger;
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _httpClientFactory = httpClientFactory;
    }

    protected override void SubscribeToEvents()
    {
        Subscribe<InvoiceEvent>();
        base.SubscribeToEvents();
    }

    protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
    {
        if (evt is InvoiceEvent invoiceEvent && new[]
            {
                    InvoiceEvent.MarkedCompleted,
                    InvoiceEvent.MarkedInvalid,
                    InvoiceEvent.Expired,
                    InvoiceEvent.Confirmed,
                    InvoiceEvent.Completed
                }.Contains(invoiceEvent.Name))
        {
            var invoice = invoiceEvent.Invoice;
            var shopifyOrderId = invoice.GetInternalTags("7").FirstOrDefault();
            if (shopifyOrderId != null)
            {
                string invoiceStatus = invoice.Status.ToString().ToLower();
                bool? success = invoiceStatus switch
                {
                    _ when new[] { "complete", "confirmed", "paid", "settled" }.Contains(invoiceStatus) => true,
                    _ when new[] { "invalid", "expired" }.Contains(invoiceStatus) => false,
                    _ => (bool?)null
                };
            }
        }
        await base.ProcessEvent(evt, cancellationToken);
    }

}
