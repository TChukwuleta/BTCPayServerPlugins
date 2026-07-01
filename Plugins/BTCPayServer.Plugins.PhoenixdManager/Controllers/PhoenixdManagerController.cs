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
            vm.Offer = SafeString(await TryAsync(() => _client.GetOffer()));
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

    [HttpPost("receive")]
    public async Task<IActionResult> CreateInvoice(long? amountSat, string description, string? externalId)
    {
        try
        {
            var inv = await _client.CreateInvoice(amountSat, description ?? "", externalId);
            TempData["SuccessMessage"] = $"Invoice created: {inv?.Serialized}";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = Describe(ex); }
        return RedirectToAction(nameof(Index));
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
