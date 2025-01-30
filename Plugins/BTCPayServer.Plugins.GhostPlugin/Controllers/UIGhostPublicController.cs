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
using System.Security.Cryptography;
using System.IO;
using AngleSharp.Dom;

namespace BTCPayServer.Plugins.ShopifyPlugin;


[AllowAnonymous]
[Route("~/plugins/{storeId}/ghost/api/")]
public class UIGhostPublicController : Controller
{
    private readonly UriResolver _uriResolver;
    private readonly StoreRepository _storeRepo;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpClientFactory _clientFactory;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly ApplicationDbContextFactory _context;
    private readonly UIInvoiceController _invoiceController;
    private readonly GhostDbContextFactory _dbContextFactory;
    private GhostHelper helper;
    public UIGhostPublicController
        (UriResolver uriResolver,
        StoreRepository storeRepo,
        LinkGenerator linkGenerator,
        IHttpClientFactory clientFactory,
        ApplicationDbContextFactory context,
        InvoiceRepository invoiceRepository,
        UIInvoiceController invoiceController,
        GhostDbContextFactory dbContextFactory)
    {
        helper = new GhostHelper();
        _context = context;
        _storeRepo = storeRepo;
        _uriResolver = uriResolver;
        _linkGenerator = linkGenerator;
        _clientFactory = clientFactory;
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _invoiceController = invoiceController;
    }

    private const string GHOST_MEMBER_ID_PREFIX = "Ghost_member-";

    [HttpGet("donate")]
    public async Task<IActionResult> Donate(string storeId)
    {

        var store = await _storeRepo.FindStore(storeId);
        if (store == null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == storeId);
        if (ghostSetting == null || !ghostSetting.CredentialsPopulated())
            return NotFound();

        var apiClient = new GhostAdminApiClient(_clientFactory, ghostSetting.CreateGhsotApiCredentials());
        var ghostSettings = await apiClient.RetrieveGhostSettings();
        Console.WriteLine(JsonConvert.SerializeObject(ghostSettings));

        var donationsCurrency = ghostSettings.FirstOrDefault(s => s.key == "donations_currency")?.value?.ToString();
        Console.WriteLine($"Donation currency: {donationsCurrency}");
        donationsCurrency ??= "USD";

        string id = Guid.NewGuid().ToString();

        InvoiceEntity invoice = await _invoiceController.CreateInvoiceCoreRaw(new CreateInvoiceRequest()
        {
            Amount = null,
            Currency = donationsCurrency,
            Metadata = new JObject
            {
                ["GhostDonationUuid"] = id
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
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == storeId);
        if (ghostSetting == null || !ghostSetting.CredentialsPopulated())
            return NotFound();

        var apiClient = new GhostAdminApiClient(_clientFactory, ghostSetting.CreateGhsotApiCredentials());
        var ghostTiers = await apiClient.RetrieveGhostTiers();

        var storeData = await _storeRepo.FindStore(storeId);

        return View(new CreateMemberViewModel { 
            GhostTiers = ghostTiers, 
            StoreId = storeId, 
            StoreName = storeData?.StoreName, 
            ShopName = ghostSetting.ApiUrl,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, storeData?.GetStoreBlob()),
        });
    }


    [HttpPost("create-member")]
    public async Task<IActionResult> CreateMember(CreateMemberViewModel vm, string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == storeId);
        if (ghostSetting == null || !ghostSetting.CredentialsPopulated())
            return NotFound();

        var storeData = await _storeRepo.FindStore(storeId);
        var apiClient = new GhostAdminApiClient(_clientFactory, ghostSetting.CreateGhsotApiCredentials());
        var ghostTiers = await apiClient.RetrieveGhostTiers();
        Tier tier = ghostTiers.FirstOrDefault(c => c.id == vm.TierId);
        if (tier == null)
            return NotFound();

        GhostMember entity = new GhostMember
        {
            CreatedAt = DateTime.UtcNow,
            Name = vm.Name,
            Email = vm.Email,
            Frequency = vm.TierSubscriptionFrequency,
            TierId = vm.TierId,
            StoreId = storeId
        };
        ctx.Add(entity);
        await ctx.SaveChangesAsync();
        InvoiceEntity invoice = await CreateInvoiceAsync(storeData, tier, entity);
        GhostTransaction transaction = new GhostTransaction
        {
            StoreId = storeId,
            InvoiceId = invoice.Id,
            MemberId = entity.Id,
            TransactionStatus = GhostPlugin.Data.TransactionStatus.Pending,
            TierId = vm.TierId,
            Frequency = vm.TierSubscriptionFrequency,
            CreatedAt = DateTime.UtcNow,
            Amount = (decimal)(vm.TierSubscriptionFrequency == TierSubscriptionFrequency.Monthly ? tier.monthly_price : tier.yearly_price)
        };
        ctx.Add(transaction);
        await ctx.SaveChangesAsync();

        await using var dbMain = _context.CreateContext();
        var store = await dbMain.Stores.SingleOrDefaultAsync(a => a.Id == storeId);

        return View("InitiatePayment", new GhostOrderViewModel
        {
            StoreId = storeId,
            StoreName = store.StoreName,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, store.GetStoreBlob()),
            BTCPayServerUrl = Request.GetAbsoluteRoot(),
            RedirectUrl = $"https://{ghostSetting.ApiUrl}/#/portal/signin",
            InvoiceId = invoice.Id
        });
    }


    [HttpGet("btcpay-ghost.js")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> GetBtcPayJavascript(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var userStore = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == storeId);
        if (userStore == null || !userStore.CredentialsPopulated())
        {
            return BadRequest("Invalid BTCPay store specified");
        }
        var jsFile = await helper.GetCustomJavascript(userStore.StoreId, Request.GetAbsoluteRoot());
        if (!jsFile.succeeded)
        {
            return BadRequest(jsFile.response);
        }
        return Content(jsFile.response, "text/javascript");
    }


    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveWebhook(string storeId)
    {
        Console.WriteLine($"Store Id: {storeId}");
        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var requestBody = await reader.ReadToEndAsync();
            var webhookResponse = JsonConvert.DeserializeObject<GhostWebhookResponse>(requestBody);

            await using var ctx = _dbContextFactory.CreateContext();
            var webhookMember = webhookResponse.member;
            string memberId = webhookMember?.previous?.id ?? webhookMember?.current?.id;
            if (string.IsNullOrEmpty(memberId))
                return NotFound();

            var member = ctx.GhostMembers.AsNoTracking().FirstOrDefault(c => c.MemberId == memberId);
            var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == storeId);
            if (member == null || ghostSetting == null)
                return NotFound();

            // Ghost webhook doesn't contain the kind of event triggered.But contains two member objects: previous and current.
            // These objects represents the previous state before the event and the state afterwards. With this I am inferring the event type:
            // If "previous" exists but "current" does not → Member was deleted(member.deleted)
            // If both "previous" and "current" exist → Member was updated(member.updated)
            if (webhookMember.previous != null && webhookMember.current == null)
            {
                var transactions = ctx.GhostTransactions.AsNoTracking().Where(c => c.MemberId == member.Id).ToList();
                if (transactions.Any())
                    ctx.RemoveRange(transactions);

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

    private async Task<InvoiceEntity> CreateInvoiceAsync(Data.StoreData store, Tier tier, GhostMember member)
    {
        var ghostSearchTerm = $"{GHOST_MEMBER_ID_PREFIX}{member.Id}";
        var matchedExistingInvoices = await _invoiceRepository.GetInvoices(new InvoiceQuery()
        {
            TextSearch = ghostSearchTerm,
            StoreId = new[] { store.Id }
        });

        matchedExistingInvoices = matchedExistingInvoices.Where(entity =>
                entity.GetInternalTags(ghostSearchTerm).Any(s => s == member.Id.ToString())).ToArray();

        var firstInvoiceSettled =
            matchedExistingInvoices.LastOrDefault(entity =>
                new[] { "settled", "processing", "confirmed", "paid", "complete" }
                    .Contains(
                        entity.GetInvoiceState().Status.ToString().ToLower()));

        if (firstInvoiceSettled != null)
            return firstInvoiceSettled;

        var invoice = await _invoiceController.CreateInvoiceCoreRaw(
            new CreateInvoiceRequest()
            {
                Amount = member.Frequency == TierSubscriptionFrequency.Monthly ? tier.monthly_price : tier.yearly_price,
                Currency = tier.currency,
                Metadata = new JObject
                {
                    ["MemberId"] = member.Id
                },
                AdditionalSearchTerms = new[]
                {
                        member.Id.ToString(CultureInfo.InvariantCulture),
                        ghostSearchTerm
                }
            }, store,
            Request.GetAbsoluteRoot(), new List<string>() { ghostSearchTerm });

        return invoice;
    }
}
