using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Data;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Services.Stores;
using BTCPayServer.Plugins.GhostPlugin.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Net.Http;
using BTCPayServer.Models;
using BTCPayServer.Services;
using System.Collections.Generic;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Controllers;
using Newtonsoft.Json.Linq;
using System.Globalization;
using BTCPayServer.Client.Models;
using Microsoft.AspNetCore.Cors;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Plugins.GhostPlugin.Data;
using BTCPayServer.Plugins.GhostPlugin.Helper;
using BTCPayServer.Plugins.GhostPlugin.ViewModels;
using BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;
using BTCPayServer.Controllers.Greenfield;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using System.Text;
using System.IO;
using BTCPayServer.Plugins.GhostPlugin;
using NBitcoin.DataEncoders;
using NBitcoin;
using System.Security.Cryptography;

namespace BTCPayServer.Plugins.ShopifyPlugin;

// This api route is used in GhostPluginService ... If you change here, go change there too
[AllowAnonymous]
[Route("~/plugins/{storeId}/ghost/public/", Order = 0)]
[Route("~/plugins/{storeId}/ghost/api/", Order = 1)]
public class UIGhostPublicController : Controller
{
    private readonly UriResolver _uriResolver;
    private readonly StoreRepository _storeRepo;
    private readonly EmailService _emailService;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpClientFactory _clientFactory;
    private readonly ApplicationDbContextFactory _context;
    private readonly GhostPluginService _ghostPluginService;
    private readonly UIInvoiceController _invoiceController;
    private readonly GhostDbContextFactory _dbContextFactory;
    public UIGhostPublicController
        (EmailService emailService,
        UriResolver uriResolver,
        StoreRepository storeRepo,
        LinkGenerator linkGenerator,
        IHttpClientFactory clientFactory,
        ApplicationDbContextFactory context,
        GhostPluginService ghostPluginService,
        UIInvoiceController invoiceController,
        GhostDbContextFactory dbContextFactory)
    {
        _context = context;
        _storeRepo = storeRepo;
        _uriResolver = uriResolver;
        _emailService = emailService;
        _linkGenerator = linkGenerator;
        _clientFactory = clientFactory;
        _dbContextFactory = dbContextFactory;
        _invoiceController = invoiceController;
        _ghostPluginService = ghostPluginService;
    }


    [HttpGet("donate")]
    public async Task<IActionResult> Donate(string storeId)
    {
        var store = await _storeRepo.FindStore(storeId);
        if (store == null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.FirstOrDefault(c => c.StoreId == storeId);
        if (ghostSetting == null || !ghostSetting.CredentialsPopulated())
            return NotFound();

        var apiClient = new GhostAdminApiClient(_clientFactory, ghostSetting.CreateGhsotApiCredentials());
        var ghostSettings = await apiClient.RetrieveGhostSettings();

        var donationsCurrency = ghostSettings.FirstOrDefault(s => s.key == "donations_currency")?.value?.ToString();
        var storeBlob = store.GetStoreBlob();
        donationsCurrency ??= storeBlob.DefaultCurrency;
        string id = Guid.NewGuid().ToString();

        InvoiceEntity invoice = await _invoiceController.CreateInvoiceCoreRaw(new CreateInvoiceRequest()
        {
            Amount = null,
            Currency = donationsCurrency,
            Metadata = new JObject
            {
                ["GhostDonationId"] = id
            },
            AdditionalSearchTerms = new[]
            {
                    id.ToString(CultureInfo.InvariantCulture),
                    $"Ghost_{id}"
            }
        }, store, HttpContext.Request.GetAbsoluteRoot(), new List<string>() { $"Ghost_{id}" });
        var url = GreenfieldInvoiceController.ToModel(invoice, _linkGenerator, HttpContext.Request).CheckoutLink;
        return Redirect(url);
    }

    [HttpGet("create-member")]
    public async Task<IActionResult> CreateMember(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.FirstOrDefault(c => c.StoreId == storeId);
        if (ghostSetting == null || !ghostSetting.CredentialsPopulated())
            return NotFound();

        var apiClient = new GhostAdminApiClient(_clientFactory, ghostSetting.CreateGhsotApiCredentials());
        var ghostTiers = await apiClient.RetrieveGhostTiers();
        var storeData = await _storeRepo.FindStore(ghostSetting.StoreId);
        return View(new CreateMemberViewModel { 
            GhostTiers = ghostTiers, 
            StoreId = ghostSetting.StoreId, 
            StoreName = storeData?.StoreName, 
            ShopName = ghostSetting.ApiUrl,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, storeData?.GetStoreBlob()),
        });
    }


    [HttpPost("create-member")]
    public async Task<IActionResult> CreateMember(CreateMemberViewModel vm, string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.FirstOrDefault(c => c.StoreId == storeId);
        if (ghostSetting == null || !ghostSetting.CredentialsPopulated())
            return NotFound();

        var storeData = await _storeRepo.FindStore(ghostSetting.StoreId);
        var apiClient = new GhostAdminApiClient(_clientFactory, ghostSetting.CreateGhsotApiCredentials());
        var ghostTiers = await apiClient.RetrieveGhostTiers();
        if (ghostTiers == null)
            return NotFound();

        vm.GhostTiers = ghostTiers;
        vm.StoreName = storeData?.StoreName;
        vm.ShopName = ghostSetting.ApiUrl;
        Tier tier = ghostTiers.FirstOrDefault(c => c.id == vm.TierId);
        if (tier == null)
            return NotFound();

        var member = await apiClient.RetrieveMember(vm.Email);
        if (member.Any())
        {
            ModelState.AddModelError(nameof(vm.Email), "A member with this email already exist");
            return View(vm);
        }

        GhostMember entity = new GhostMember
        {
            Status = GhostSubscriptionStatus.New,
            CreatedAt = DateTime.UtcNow,
            Name = vm.Name,
            Email = vm.Email,
            Frequency = vm.TierSubscriptionFrequency,
            TierId = vm.TierId,
            TierName = tier.name,
            StoreId = ghostSetting.StoreId
        };
        ctx.GhostMembers.Add(entity);
        await ctx.SaveChangesAsync();
        var txnId = Encoders.Base58.EncodeData(RandomUtils.GetBytes(20));
        InvoiceEntity invoice = await _ghostPluginService.CreateMemberInvoiceAsync(storeData, tier, entity, txnId, Request.GetAbsoluteRoot());
        await GetTransaction(ctx, tier, entity, invoice, null, txnId);
        await using var dbMain = _context.CreateContext();
        var store = await dbMain.Stores.SingleOrDefaultAsync(a => a.Id == ghostSetting.StoreId);

        return View("InitiatePayment", new GhostOrderViewModel
        {
            StoreId = ghostSetting.StoreId,
            StoreName = store.StoreName,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, store.GetStoreBlob()),
            BTCPayServerUrl = Request.GetAbsoluteRoot(),
            RedirectUrl = $"https://{ghostSetting.ApiUrl}/#/portal/signin",
            InvoiceId = invoice.Id
        });
    }


    [HttpGet("subscription/{memberId}/subscribe")]
    public async Task<IActionResult> Subscribe(string storeId, string memberId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var member = ctx.GhostMembers.FirstOrDefault(c => c.Id == memberId && c.StoreId == storeId);
        var ghostSetting = ctx.GhostSettings.FirstOrDefault(c => c.StoreId == storeId);
        if (member == null || ghostSetting == null || !ghostSetting.CredentialsPopulated())
            return NotFound();

        var apiClient = new GhostAdminApiClient(_clientFactory, ghostSetting.CreateGhsotApiCredentials());
        var ghostTiers = await apiClient.RetrieveGhostTiers();
        if (ghostTiers == null)
            return NotFound();

        Tier tier = ghostTiers.FirstOrDefault(c => c.id == member.TierId);
        if(tier == null)
            return NotFound();

        var storeData = await _storeRepo.FindStore(storeId);
        var latestTransaction = ctx.GhostTransactions.Where(t => t.StoreId == storeId && t.TransactionStatus == GhostPlugin.Data.TransactionStatus.Settled && t.MemberId == memberId)
            .OrderByDescending(t => t.PeriodEnd).First();

        var endDate = DateTime.UtcNow.Date > latestTransaction.PeriodEnd.Date ? DateTime.UtcNow.Date.AddDays(1) : latestTransaction.PeriodEnd.AddHours(1);
        var txnId = Encoders.Base58.EncodeData(RandomUtils.GetBytes(20));
        var pr = await _ghostPluginService.CreatePaymentRequest(member, tier, ghostSetting.AppId, endDate);
        await GetTransaction(ctx, tier, member, null, pr, txnId);
        return RedirectToAction("ViewPaymentRequest", "UIPaymentRequest", new { payReqId = pr.Id });
    }


    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveWebhook(string storeId)
    {
        try
        {
            await using var ctx = _dbContextFactory.CreateContext();
            var ghostSetting = ctx.GhostSettings.FirstOrDefault(c => c.StoreId == storeId);
            if (ghostSetting == null)
                return BadRequest();

            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var requestBody = await reader.ReadToEndAsync();

            if (string.IsNullOrEmpty(requestBody))
                return BadRequest("Empty request body");

            if (!Request.Headers.TryGetValue("X-Ghost-Signature", out var signatureHeaderValues) ||
                    string.IsNullOrEmpty(signatureHeaderValues.FirstOrDefault()))
            {
                return BadRequest("Missing signature header");
            }

            if (!ValidateSignature(requestBody, signatureHeaderValues.First(), ghostSetting.WebhookSecret))
            {
                return Unauthorized("Invalid webhook signature");
            }

            var webhookResponse = JsonConvert.DeserializeObject<GhostWebhookResponse>(requestBody);
            var webhookMember = webhookResponse.member;
            string memberId = webhookMember?.previous?.id ?? webhookMember?.current?.id;
            if (string.IsNullOrEmpty(memberId))
                return NotFound();

            var member = ctx.GhostMembers.FirstOrDefault(c => c.MemberId == memberId);
            if (member == null)
                return NotFound();

            // Ghost webhook doesn't contain the kind of event triggered.But contains two member objects: previous and current.
            // These objects represents the previous state before the event and the state afterwards. With this I am inferring the event type:
            // If "previous" exists but "current" does not → Member was deleted(member.deleted)
            // If both "previous" and "current" exist → Member was updated(member.updated)
            if (webhookMember.previous != null && webhookMember.current == null)
            {
                var transactions = ctx.GhostTransactions.Where(c => c.MemberId == member.Id).ToList();
                if (transactions.Any())
                {
                    ctx.RemoveRange(transactions);
                }
                ctx.Remove(member);
                await ctx.SaveChangesAsync();
            }
            else if (webhookMember.previous != null && webhookMember.current != null)
            {
                member.Email = webhookMember.current.email;
                member.Name = webhookMember.current.name;
                ctx.Update(member);
                await ctx.SaveChangesAsync();
            }
            return Ok(new { message = "Webhook processed successfully." });
        }
        catch (Exception)
        {
            return StatusCode(500, "Internal Server Error.");
        }
    }


    [HttpGet("btcpay-ghost.js")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> GetBtcPayJavascript(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var userStore = ctx.GhostSettings.FirstOrDefault(c => c.StoreId == storeId);
        if (userStore == null || !userStore.CredentialsPopulated())
        {
            return BadRequest("Invalid BTCPay store specified");
        }
        var fileContent = _emailService.GetEmbeddedResourceContent("Resources.js.btcpay_ghost.js");
        return Content(fileContent, "text/javascript");
    }


    [HttpGet("paywall/btcpay-ghost-paywall.js")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> GetBtcPayGhostPaywallJavascript(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var userStore = ctx.GhostSettings.FirstOrDefault(c => c.StoreId == storeId);
        if (userStore == null || !userStore.CredentialsPopulated())
            return BadRequest("Invalid BTCPay store specified");

        var store = await _storeRepo.FindStore(storeId);
        if (store == null)
            return NotFound();

        var storeBlob = store.GetStoreBlob();
        StringBuilder combinedJavascript = new StringBuilder();
        var fileContent = _emailService.GetEmbeddedResourceContent("Resources.js.btcpay_paywall_ghost.js");

        combinedJavascript.AppendLine(fileContent);
        string jsVariables = $"var BTCPAYSERVER_URL = '{Request.GetAbsoluteRoot()}'; var BTCPAYSERVER_STORE_ID = '{userStore.StoreId}'; var STORE_CURRENCY = '{storeBlob.DefaultCurrency}';";
        combinedJavascript.Insert(0, jsVariables + Environment.NewLine);
        var jsFile = combinedJavascript.ToString();
        return Content(jsFile, "text/javascript");
    }


    [AllowAnonymous]
    [HttpGet("paywall/create-invoice")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> CreateOrder(string storeId, decimal amount)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var userStore = ctx.GhostSettings.FirstOrDefault(c => c.StoreId == storeId);
        if (userStore == null || !userStore.CredentialsPopulated())
        {
            return BadRequest("Invalid BTCPay store specified");
        }
        var store = await _storeRepo.FindStore(storeId);
        if (store == null)
            return NotFound();

        var storeBlob = store.GetStoreBlob();
        string orderId = string.Empty;
        InvoiceMetadata metadata = new InvoiceMetadata
        {
            OrderId = orderId,
        };
        var result = await _invoiceController.CreateInvoiceCoreRaw(new Client.Models.CreateInvoiceRequest()
        {
            Amount = amount,
            Currency = storeBlob.DefaultCurrency,
            Metadata = metadata.ToJObject(),
        }, store, HttpContext.Request.GetAbsoluteRoot());
        return Ok(new
        {
            id = result.Id,
            orderId,
            Message = "Order created and invoice generated successfully"
        });
    }

    private async Task GetTransaction(GhostDbContext ctx, Tier tier, GhostMember member, InvoiceEntity invoice, Data.PaymentRequestData paymentRequest, string txnId)
    {
        // Amount is in lower denomination, so divided by 100
        var price = Convert.ToDecimal(member.Frequency == TierSubscriptionFrequency.Monthly ? tier.monthly_price : tier.yearly_price) / 100;
        GhostTransaction transaction = new GhostTransaction
        {
            StoreId = member.StoreId,
            TxnId = txnId,
            InvoiceId = invoice?.Id,
            PaymentRequestId = paymentRequest?.Id,
            MemberId = member.Id,
            TransactionStatus = GhostPlugin.Data.TransactionStatus.New,
            TierId = member.TierId,
            Frequency = member.Frequency,
            CreatedAt = DateTime.UtcNow,
            Amount = price,
            Currency = tier.currency
        };
        ctx.GhostTransactions.Add(transaction);
        await ctx.SaveChangesAsync();
    }

    private bool ValidateSignature(string payload, string signatureHeader, string webhookSecret)
    {
        // Parse the signature header which has format: "sha256=SIGNATURE, t=TIMESTAMP"
        var headerParts = signatureHeader.Split(',');
        if (headerParts.Length != 2) return false;

        var signaturePart = headerParts[0].Trim();
        var signatureParts = signaturePart.Split('=');
        if (signatureParts.Length != 2 || signatureParts[0] != "sha256") return false;
        var providedSignature = signatureParts[1];

        var timestampPart = headerParts[1].Trim();
        var timestampParts = timestampPart.Split('=');
        if (timestampParts.Length != 2 || timestampParts[0] != "t") return false;
        var timestamp = timestampParts[1];

        string dataToSign = payload + timestamp;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
        var computedSignature = BitConverter.ToString(computedHash).Replace("-", "").ToLower();
        return SecureCompare(providedSignature, computedSignature);
    }


    private bool SecureCompare(string a, string b)
    {
        if (a.Length != b.Length) return false;

        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        return result == 0;
    }
}
