using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.SquareSpace.Helper;
using BTCPayServer.Plugins.SquareSpace.Services;
using BTCPayServer.Plugins.SquareSpace.ViewModels;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.SquareSpace;

[Route("~/plugins/{storeId}/squarespace/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UISquarespaceController : Controller
{
    private readonly StoreRepository _storeRepository;
    private readonly SquarespaceService _squarespaceService;
    private readonly LightSpeedDbContextFactory _dbContextFactory;
    public UISquarespaceController(LightSpeedDbContextFactory dbContextFactory, StoreRepository storeRepository, 
        SquarespaceService squarespaceService)
    {
        _storeRepository = storeRepository;
        _dbContextFactory = dbContextFactory;
        _squarespaceService = squarespaceService;
    }
    private StoreData CurrentStore => HttpContext.GetStoreData();

    [HttpGet("settings")]
    public async Task<IActionResult> Index(string storeId)
    {
        if (CurrentStore == null) return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var squareSpaceSettings = ctx.SquareSpaceSettings.FirstOrDefault(c => c.StoreId == CurrentStore.Id) ?? new();
        var vm = squareSpaceSettings.SquareSpaceSettingsToViewModel();
        vm.CodeInjectionUrl = Url.Action(nameof(UISquarespacePublicController.GetBtcPayJavascript), "UISquarespacePublic", new { storeId }, Request.Scheme);
        return View(vm);
    }

    [HttpPost("settings/update")]
    public async Task<IActionResult> UpdateSettings(string storeId, SquarespaceSettingsVm vm)
    {
        if (CurrentStore == null) return NotFound();

        if (!ModelState.IsValid) 
            return View("/Views/UISquareSpace/Index.cshtml", vm);

        await using var ctx = _dbContextFactory.CreateContext();
        var squareSpaceSettings = ctx.SquareSpaceSettings.FirstOrDefault(c => c.StoreId == CurrentStore.Id);

        // If OAuth token or webhook endpoint changed, recreate webhook subscription
        if (squareSpaceSettings == null || squareSpaceSettings.OAuthToken != vm.OAuthToken 
            || squareSpaceSettings.WebhookEndpointUrl != vm.WebhookEndpointUrl)
        {
            if (!string.IsNullOrEmpty(squareSpaceSettings?.WebhookSubscriptionId) && !string.IsNullOrEmpty(squareSpaceSettings?.OAuthToken))
            {
                await _squarespaceService.DeleteWebhookSubscription(squareSpaceSettings.OAuthToken, squareSpaceSettings.WebhookSubscriptionId);
            }

            vm.WebhookEndpointUrl = Url.Action(nameof(UISquarespacePublicController.Webhook), "UISquarespacePublic", new { storeId }, Request.Scheme); // use nameOf

            var createWebhookSubscription = await _squarespaceService.CreateWebhookSubscription(vm.OAuthToken, vm.WebhookEndpointUrl);
            if (createWebhookSubscription  == null)
            {
                TempData[WellKnownTempData.ErrorMessage] = "Failed to create webhook";
                return View("/Views/UISquareSpace/Index.cshtml", vm);
            }
            vm.WebhookSubscriptionId = createWebhookSubscription.SubscriptionId;
            vm.WebhookSecret = createWebhookSubscription.Secret;
            squareSpaceSettings = vm.SquareSpaceViewModelToSettings();
        }
        ctx.Update(squareSpaceSettings);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Squarespace settings updated successfully";
        return RedirectToAction(nameof(Index), new { storeId });
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(string storeId)
    {
        var store = await _storeRepository.FindStore(storeId);
        if (store == null) return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var settings = ctx.SquareSpaceSettings.FirstOrDefault(c => c.StoreId == CurrentStore.Id);
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
