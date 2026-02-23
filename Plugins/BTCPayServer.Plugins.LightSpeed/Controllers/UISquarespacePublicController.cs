using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.SquareSpace.Data;
using BTCPayServer.Plugins.SquareSpace.Services;
using BTCPayServer.Plugins.SquareSpace.ViewModels;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.SquareSpace;

[AllowAnonymous]
[Route("~/plugins/{storeId}/squarespace/public/")]
public class UISquarespacePublicController : Controller
{
    private readonly ILogger<UISquarespacePublicController> _logger;
    private readonly StoreRepository _storeRepository;
    private readonly SquarespaceService _squarespaceService;
    private readonly LightSpeedDbContextFactory _dbContextFactory;
    public UISquarespacePublicController(LightSpeedDbContextFactory dbContextFactory, StoreRepository storeRepository, 
        SquarespaceService squarespaceService, ILogger<UISquarespacePublicController> logger)
    {
        _logger = logger;
        _storeRepository = storeRepository;
        _dbContextFactory = dbContextFactory;
        _squarespaceService = squarespaceService;
    }

    [HttpGet("btcpay-ghost.js")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> GetBtcPayJavascript(string storeId)
    {
        var store = await _storeRepository.FindStore(storeId);
        if (store == null) return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var settings = ctx.SquareSpaceSettings.FirstOrDefault(c => c.StoreId == store.Id);
        /*if (settings == null || string.IsNullOrEmpty(settings.WebhookSecret))
            return BadRequest("Squarespace not configured");*/

        var storeBlob = store.GetStoreBlob();
        StringBuilder combinedJavascript = new StringBuilder();
        var fileContent = _squarespaceService.GetEmbeddedResourceContent("Resources.js.btcpay_squarespace.js");
        combinedJavascript.AppendLine(fileContent);
        string jsVariables = $"var BTCPAYSERVER_URL = '{Request.GetAbsoluteRoot()}'; var BTCPAYSERVER_STORE_ID = '{store.Id}'; var STORE_CURRENCY = '{storeBlob.DefaultCurrency}';";
        combinedJavascript.Insert(0, jsVariables + Environment.NewLine);
        var jsFile = combinedJavascript.ToString();
        return Content(jsFile, "application/javascript; charset=utf-8");
    }

    [HttpPost("cart")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> Cart(string storeId, [FromBody] SquareSpaceCheckoutRequest request)
    {
        _logger.LogInformation(JsonConvert.SerializeObject(request));
        var store = await _storeRepository.FindStore(storeId);
        if (store == null) return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var settings = ctx.SquareSpaceSettings.FirstOrDefault(c => c.StoreId == store.Id);
        /*if (settings == null || string.IsNullOrEmpty(settings.WebhookSecret))
            return BadRequest("Squarespace not configured");*/

        var paymentUrl = Url.Action(nameof(CompleteCheckout), "UISquarespacePublic", new { storeId, cartToken = request.CartToken }, Request.Scheme);
        var existingOrder = ctx.SquareSpaceOrders.FirstOrDefault(c => c.CartToken == request.CartToken && c.StoreId == store.Id);
        if (existingOrder != null)
            return Ok(new { paymentUrl });

        var squareSpaceOrder = new SquareSpaceOrder
        {
            StoreId = store.Id,
            CartData = request.CartData,
            CartId = request.CartId,
            CartToken = request.CartToken,
            Amount = request.Amount,
            Items = JsonConvert.SerializeObject(request.Items)
        };
        ctx.SquareSpaceOrders.Add(squareSpaceOrder);
        await ctx.SaveChangesAsync();
        return Ok(new { paymentUrl });
    }

    [HttpGet("{cartToken}/complete-checkout")]
    public async Task<IActionResult> CompleteCheckout(string storeId, string cartToken)
    {
        var store = await _storeRepository.FindStore(storeId);
        if (store == null) return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var settings = ctx.SquareSpaceSettings.FirstOrDefault(c => c.StoreId == store.Id);
        /*if (settings == null || string.IsNullOrEmpty(settings.WebhookSecret))
            return NotFound();*/

        var order = ctx.SquareSpaceOrders.FirstOrDefault(c => c.StoreId == store.Id && c.CartToken == cartToken);
        if (order == null) return NotFound();

        return View(new SquareSpaceCheckoutViewModel
        {
            StoreId = storeId,
            CartToken = cartToken,
            Amount = order.Amount,
            Items = JsonConvert.DeserializeObject<List<InvoiceItem>>(order.Items)
        });
    }

    [HttpPost("{cartToken}/complete-checkout")]
    public async Task<IActionResult> CompleteCheckout(string storeId, string cartToken, SquareSpaceCheckoutViewModel model)
    {
        var store = await _storeRepository.FindStore(storeId);
        if (store == null) return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var settings = ctx.SquareSpaceSettings.FirstOrDefault(c => c.StoreId == store.Id);
        /*if (settings == null || string.IsNullOrEmpty(settings.WebhookSecret))
            return NotFound();*/

        var order = ctx.SquareSpaceOrders.FirstOrDefault(c => c.StoreId == store.Id && c.CartToken == cartToken);
        if (order == null) return NotFound();

        order.CustomerEmail = model.Email;
        order.ShippingAddress = JsonConvert.SerializeObject(new ShippingAddress
        {
            Address1 = model.Address1,
            Address2 = model.Address2,
            ShippingName = model.ShippingName,
            City = model.City,
            PostalCode = model.PostalCode,
            Country = model.Country,
        });
        await ctx.SaveChangesAsync();

        // Create Order... on square.. space..

        // Create BTCPay server


        //model.InvoiceUrl = invoiceUrl;
        model.Items = JsonConvert.DeserializeObject<List<InvoiceItem>>(order.Items);
        model.Amount = order.Amount;
        return View("/Views/UISquarespacePublic/CompleteCheckout.cshtml", model);
    }



    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(string storeId)
    {
        var store = await _storeRepository.FindStore(storeId);
        if (store == null) return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var settings = ctx.SquareSpaceSettings.FirstOrDefault(c => c.StoreId == store.Id);
        if (settings == null || string.IsNullOrEmpty(settings.WebhookSecret))
            return BadRequest("Squarespace not configured");

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        if (Request.Headers.TryGetValue("Squarespace-Signature", out var signatureHeader))
        {
            if (!_squarespaceService.VerifyWebhookSignature(body, signatureHeader.ToString(), settings.WebhookSecret))
            {
                return Unauthorized("Invalid signature");
            }
        }
        else
        {
            return BadRequest("Missing signature");
        }
        var notification = JsonConvert.DeserializeObject<SquarespaceWebhookNotification>(body);
        if (notification == null) return BadRequest("Invalid payload");

        // Only process order.create events
        if (notification.Topic == "order.create" && settings.AutoCreateInvoices)
        {
            //await ProcessOrderCreate(storeId, notification.Data, settings);
        }
        return Ok();
    }

    /*private async Task ProcessOrderCreate(string storeId, SquarespaceOrderData orderData, SquarespaceSettings settings)
    {
        try
        {
            // Check if we already created an invoice for this order
            var existingMapping = await _dbContext.Set<SquarespaceOrderMapping>()
                .FirstOrDefaultAsync(m => m.SquarespaceOrderId == orderData.Id);

            if (existingMapping != null)
                return; // Already processed

            // Create BTCPay invoice
            var invoiceRequest = new CreateInvoiceRequest
            {
                Amount = orderData.GrandTotal,
                Currency = orderData.Currency,
                Metadata = new Dictionary<string, string>
                {
                    ["orderId"] = orderData.Id,
                    ["orderNumber"] = orderData.OrderNumber,
                    ["source"] = "squarespace"
                },
                Checkout = new InvoiceDataBase.CheckoutOptions
                {
                    Expiration = TimeSpan.FromMinutes(settings.InvoiceExpirationMinutes),
                    RedirectURL = $"https://www.squarespace.com/commerce/orders/{orderData.Id}",
                    PaymentMethods = null // Use store defaults
                }
            };

            // Add buyer info if available
            if (!string.IsNullOrEmpty(orderData.CustomerEmail))
            {
                invoiceRequest.Metadata["buyerEmail"] = orderData.CustomerEmail;
            }

            if (!string.IsNullOrEmpty(orderData.BillingAddress?.FirstName))
            {
                invoiceRequest.Metadata["buyerName"] =
                    $"{orderData.BillingAddress.FirstName} {orderData.BillingAddress.LastName}";
            }

            // Add line items as description
            var itemDescriptions = orderData.LineItems
                .Select(item => $"{item.Quantity}x {item.ProductName}")
                .ToList();

            if (itemDescriptions.Any())
            {
                invoiceRequest.Metadata["itemDesc"] = string.Join(", ", itemDescriptions);
            }

            var invoice = await _invoiceRepository.CreateInvoiceAsync(storeId, invoiceRequest);

            // Store the mapping
            var mapping = new SquarespaceOrderMapping
            {
                StoreId = storeId,
                SquarespaceOrderId = orderData.Id,
                SquarespaceOrderNumber = orderData.OrderNumber,
                BTCPayInvoiceId = invoice.Id,
                Status = "Pending"
            };

            await _dbContext.Set<SquarespaceOrderMapping>().AddAsync(mapping);
            await _dbContext.SaveChangesAsync();

            // TODO: Optionally send invoice link to customer via email
            // or update Squarespace order with custom field containing payment link
        }
        catch (Exception ex)
        {
            // Log error but don't fail the webhook
            Console.WriteLine($"Error processing Squarespace order: {ex.Message}");
        }
    }*/
}
