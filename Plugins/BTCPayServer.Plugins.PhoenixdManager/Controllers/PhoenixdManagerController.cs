using System;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Plugins.PhoenixdManager.Services;
using BTCPayServer.Plugins.PhoenixdManager.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.PhoenixdManager;

[Route("~/plugins/phoenixd/manage/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanModifyServerSettings)]
[AutoValidateAntiforgeryToken]
public class PhoenixdManagerController(PhoenixdClient _client, PhoenixdSettingsService _settings) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var vm = new DashboardViewModel();
        if (!await _settings.IsConfigured())
        {
            vm.Configured = false;
            return View(vm);
        }

        vm.Configured = true;
        vm.ServerUrl = (await _settings.GetSettings()).ServerUrl;
        try
        {
            vm.NodeInfo = await _client.GetInfo();
            vm.Balance = await _client.GetBalance();
            vm.Channels = (await _client.ListChannels()) ?? new();
            vm.LnAddress = SafeString(await TryAsync(() => _client.GetLnAddress()));
            vm.RecentIncoming = (await _client.ListIncomingPayments(all: true, null, limit: 20, offset: 0)) ?? new();
            vm.RecentOutgoing = (await _client.ListOutgoingPayments(all: true, limit: 20, offset: 0)) ?? new();
        }
        catch (PhoenixdApiException ex)
        {
            vm.Error = $"phoenixd API error {ex.StatusCode}: {ex.Body}";
        }
        catch (Exception ex)
        {
            vm.Error = ex.Message;
        }
        return View(vm);
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Settings()
    {
        return View(await _settings.GetSettings());
    }

    [HttpPost("settings")]
    public async Task<IActionResult> Settings(PhoenixdSettings model, string? command)
    {
        if (command == "test")
        {
            await _settings.SetSettings(model);
            var (ok, message) = await _client.TestConnection();
            TempData[ok ? "SuccessMessage" : "ErrorMessage"] = message;
            return View(model);
        }
        await _settings.SetSettings(model);
        TempData["SuccessMessage"] = "Settings saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("send")]
    public async Task<IActionResult> Send()
    {
        return View(new SendViewModel { Configured = await _settings.IsConfigured() });
    }

    [HttpPost("send/invoice")]
    public async Task<IActionResult> SendInvoice(string invoice, long? amountSat)
    {
        var vm = new SendViewModel { Configured = await _settings.IsConfigured(), ActiveTab = "invoice" };
        try
        {
            var r = await _client.PayInvoice(invoice, amountSat);
            FillResult(vm, r);
        }
        catch (Exception ex) { vm.Error = Describe(ex); }
        return View(nameof(Send), vm);
    }

    [HttpPost("send/offer")]
    public async Task<IActionResult> SendOffer(string offer, long amountSat, string? message)
    {
        var vm = new SendViewModel { Configured = await _settings.IsConfigured(), ActiveTab = "offer" };
        try
        {
            var r = await _client.PayOffer(offer, amountSat, message);
            FillResult(vm, r);
        }
        catch (Exception ex) { vm.Error = Describe(ex); }
        return View(nameof(Send), vm);
    }

    [HttpPost("send/address")]
    public async Task<IActionResult> SendAddress(string address, long amountSat, string? message)
    {
        var vm = new SendViewModel { Configured = await _settings.IsConfigured(), ActiveTab = "address" };
        try
        {
            var r = await _client.PayLnAddress(address, amountSat, message);
            FillResult(vm, r);
        }
        catch (Exception ex) { vm.Error = Describe(ex); }
        return View(nameof(Send), vm);
    }

    private static void FillResult(SendViewModel vm, PaymentResult? r)
    {
        if (r is null) return;
        vm.HasResult = true;
        vm.RecipientAmountSat = r.RecipientAmountSat;
        vm.RoutingFeeSat = r.RoutingFeeSat;
        vm.PaymentHash = r.PaymentHash;
        vm.PaymentPreimage = r.PaymentPreimage;
    }


    [HttpGet("actions")]
    public async Task<IActionResult> Action()
    {
        var vm = new DashboardViewModel();
        if (!await _settings.IsConfigured()) 
        { 
            vm.Configured = false; return View(vm); 
        }
        vm.Configured = true;
        try { 
            vm.Channels = (await _client.ListChannels()) ?? new(); 
        }
        catch (Exception ex) { vm.Error = ex.Message; }
        return View(vm);
    }

    [HttpGet("receive")]
    public async Task<IActionResult> Receive()
    {
        var vm = new ReceiveViewModel { Configured = await _settings.IsConfigured() };
        return View(vm);
    }

    [HttpPost("receive")]
    public async Task<IActionResult> Receive(long? amountSat, string description, string? externalId)
    {
        var vm = new ReceiveViewModel { Configured = await _settings.IsConfigured() };
        try
        {
            var inv = await _client.CreateInvoice(amountSat, description ?? "", externalId);
            vm.Invoice = inv?.Serialized;
            vm.AmountSat = inv?.AmountSat;
            vm.Description = description;
            vm.PaymentHash = inv?.PaymentHash;
        }
        catch (Exception ex)
        {
            vm.Error = Describe(ex);
        }
        return View(vm);
    }

    [HttpGet("onchain")]
    public async Task<IActionResult> OnChain()
    {
        var vm = new OnChainViewModel { Configured = await _settings.IsConfigured() };
        if (vm.Configured)
        {
            try 
            { 
                vm.Chain = (await _client.GetInfo())?.Chain; 
            } catch {  }
        }
        return View(vm);
    }

    [HttpPost("onchain")]
    public async Task<IActionResult> OnChain(string address, long amountSat, int feerateSatByte)
    {
        var vm = new OnChainViewModel { Configured = await _settings.IsConfigured() };
        try
        {
            var txid = await _client.SendToAddress(address, amountSat, feerateSatByte);
            vm.HasResult = !string.IsNullOrWhiteSpace(txid);
            vm.TxId = txid?.Trim();
            vm.AmountSat = amountSat;
            vm.Address = address;
            vm.Chain = (await _client.GetInfo())?.Chain;
        }
        catch (Exception ex)
        {
            vm.Error = Describe(ex);
        }
        return View(vm);
    }


    [HttpPost("pay/invoice")]
    public async Task<IActionResult> PayInvoice(string invoice, long? amountSat)
    {
        try
        {
            var r = await _client.PayInvoice(invoice, amountSat);
            TempData["SuccessMessage"] = $"Paid. Preimage {r?.PaymentPreimage}, routing fee {r?.RoutingFeeSat} sat.";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = Describe(ex); }
        return RedirectToAction(nameof(Index));
    }


    [HttpPost("pay/offer")]
    public async Task<IActionResult> PayOffer(string offer, long amountSat, string? message)
    {
        try
        {
            var r = await _client.PayOffer(offer, amountSat, message);
            TempData["SuccessMessage"] = $"Paid offer. Preimage {r?.PaymentPreimage}, fee {r?.RoutingFeeSat} sat.";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = Describe(ex); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("pay/lnaddress")]
    public async Task<IActionResult> PayLnAddress(string address, long amountSat, string? message)
    {
        try
        {
            var r = await _client.PayLnAddress(address, amountSat, message);
            TempData["SuccessMessage"] = $"Paid {address}. Preimage {r?.PaymentPreimage}, fee {r?.RoutingFeeSat} sat.";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = Describe(ex); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("pay/onchain")]
    public async Task<IActionResult> SendToAddress(string address, long amountSat, int feerateSatByte)
    {
        try
        {
            var txid = await _client.SendToAddress(address, amountSat, feerateSatByte);
            TempData["SuccessMessage"] = $"Broadcast on-chain. txid: {txid}";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = Describe(ex); }
        return RedirectToAction(nameof(Index));
    }


    [HttpPost("decode")]
    public async Task<IActionResult> Decode(string value)
    {
        try
        {
            value = (value ?? "").Trim();
            var isOffer = value.StartsWith("lno", StringComparison.OrdinalIgnoreCase);
            var decoded = isOffer ? await _client.DecodeOffer(value) : await _client.DecodeInvoice(value);
            TempData["SuccessMessage"] =
                $"Decoded: amount={decoded?.Amount} desc=\"{decoded?.Description}\" hash={decoded?.PaymentHash}";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = Describe(ex); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("liquidity/estimate")]
    public async Task<IActionResult> EstimateLiquidity(long amountSat)
    {
        try
        {
            var est = await _client.EstimateLiquidityFees(amountSat);
            TempData["SuccessMessage"] = $"Estimated liquidity for {amountSat} sat: mining {est?.MiningFeeSat} sat, service {est?.ServiceFeeSat} sat.";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = Describe(ex); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("channels/close")]
    public async Task<IActionResult> CloseChannel(string channelId, string address, int feerateSatByte)
    {
        try
        {
            var res = await _client.CloseChannel(channelId, address, feerateSatByte);
            TempData["SuccessMessage"] = $"Close initiated: {res}";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = Describe(ex); }
        return RedirectToAction(nameof(Index));
    }


    private static string Describe(Exception ex) => ex is PhoenixdApiException p ? $"phoenixd error {p.StatusCode}: {p.Body}" : ex.Message;

    private static string? SafeString(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static async Task<T?> TryAsync<T>(Func<Task<T>> f)
    {
        try { return await f(); } catch { return default; }
    }
}
