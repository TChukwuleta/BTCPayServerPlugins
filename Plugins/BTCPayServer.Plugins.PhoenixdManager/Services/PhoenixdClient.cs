using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Plugins.PhoenixdManager.ViewModels;

namespace BTCPayServer.Plugins.PhoenixdManager.Services;

public class PhoenixdClient
{
    public const string HttpClientName = "PhoenixdManager";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PhoenixdSettingsService _settingsService;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PhoenixdClient(IHttpClientFactory httpClientFactory, PhoenixdSettingsService settingsService)
    {
        _httpClientFactory = httpClientFactory;
        _settingsService = settingsService;
    }

    private async Task<(HttpClient client, Uri baseUri)> CreateClientAsync(bool preferLimited = false)
    {
        var settings = await _settingsService.GetSettings();
        if (string.IsNullOrWhiteSpace(settings.ServerUrl))
            throw new InvalidOperationException("phoenixd server URL is not configured.");

        var client = _httpClientFactory.CreateClient(HttpClientName);

        var password = preferLimited && !string.IsNullOrWhiteSpace(settings.LimitedAccessPassword)
            ? settings.LimitedAccessPassword!
            : settings.Password;

        // phoenixd basic auth: empty username, password = http-password
        var raw = Encoding.UTF8.GetBytes($":{password}");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(raw));

        var baseUrl = settings.ServerUrl.TrimEnd('/') + "/";
        return (client, new Uri(baseUrl));
    }

    private static FormUrlEncodedContent Form(IEnumerable<KeyValuePair<string, string?>> pairs)
    {
        var filtered = new List<KeyValuePair<string, string>>();
        foreach (var p in pairs)
        {
            if (p.Value is not null)
                filtered.Add(new KeyValuePair<string, string>(p.Key, p.Value));
        }
        return new FormUrlEncodedContent(filtered);
    }

    private static string BuildQuery(IEnumerable<KeyValuePair<string, string?>> pairs)
    {
        var parts = new List<string>();
        foreach (var p in pairs)
        {
            if (p.Value is not null)
                parts.Add($"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}");
        }
        var q = string.Join("&", parts);
        return string.IsNullOrEmpty(q) ? "" : "?" + q;
    }

    private async Task<string> ReadOrThrowAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            // phoenixd returns a plain-text or JSON error body; surface it verbatim.
            throw new PhoenixdApiException((int)resp.StatusCode, body);
        }
        return body;
    }

    private async Task<T?> GetJson<T>(string path, string query, CancellationToken ct, bool preferLimited = false)
    {
        var (client, baseUri) = await CreateClientAsync(preferLimited);
        using var resp = await client.GetAsync(new Uri(baseUri, path + query), ct);
        var body = await ReadOrThrowAsync(resp, ct);
        return JsonSerializer.Deserialize<T>(body, JsonOpts);
    }

    private async Task<string> GetString(string path, string query, CancellationToken ct, bool preferLimited = false)
    {
        var (client, baseUri) = await CreateClientAsync(preferLimited);
        using var resp = await client.GetAsync(new Uri(baseUri, path + query), ct);
        return await ReadOrThrowAsync(resp, ct);
    }

    private async Task<T?> PostJson<T>(string path, IEnumerable<KeyValuePair<string, string?>> form, CancellationToken ct)
    {
        var (client, baseUri) = await CreateClientAsync();
        using var resp = await client.PostAsync(new Uri(baseUri, path), Form(form), ct);
        var body = await ReadOrThrowAsync(resp, ct);
        return JsonSerializer.Deserialize<T>(body, JsonOpts);
    }

    private async Task<string> PostString(string path, IEnumerable<KeyValuePair<string, string?>> form, CancellationToken ct)
    {
        var (client, baseUri) = await CreateClientAsync();
        using var resp = await client.PostAsync(new Uri(baseUri, path), Form(form), ct);
        return await ReadOrThrowAsync(resp, ct);
    }

    public Task<NodeInfo?> GetInfo(CancellationToken ct = default) => GetJson<NodeInfo>("getinfo", "", ct, preferLimited: true);

    public Task<BalanceInfo?> GetBalance(CancellationToken ct = default) => GetJson<BalanceInfo>("getbalance", "", ct, preferLimited: true);

    public Task<List<ChannelInfo>?> ListChannels(CancellationToken ct = default) => GetJson<List<ChannelInfo>>("listchannels", "", ct, preferLimited: true);

    public Task<string> GetOffer(CancellationToken ct = default) => GetString("getoffer", "", ct, preferLimited: true);

    public Task<string> GetLnAddress(CancellationToken ct = default) => GetString("getlnaddress", "", ct, preferLimited: true);

    public Task<LiquidityFeeEstimate?> EstimateLiquidityFees(long amountSat, CancellationToken ct = default)
        => GetJson<LiquidityFeeEstimate>("estimateliquidityfees",
            BuildQuery(new[] { new KeyValuePair<string, string?>("amountSat", amountSat.ToString()) }), ct, preferLimited: true);


    public Task<CreatedInvoice?> CreateInvoice(long? amountSat, string description, string? externalId, CancellationToken ct = default)
    {
        var form = new List<KeyValuePair<string, string?>>
        {
            new("description", description),
            new("externalId", externalId),
        };
        if (amountSat.HasValue) form.Add(new("amountSat", amountSat.Value.ToString()));
        return PostJson<CreatedInvoice>("createinvoice", form, ct);
    }

    public Task<PaymentResult?> PayInvoice(string invoice, long? amountSat, CancellationToken ct = default)
    {
        var form = new List<KeyValuePair<string, string?>>
        {
            new("invoice", invoice),
        };
        if (amountSat.HasValue) form.Add(new("amountSat", amountSat.Value.ToString()));
        return PostJson<PaymentResult>("payinvoice", form, ct);
    }

    public Task<PaymentResult?> PayOffer(string offer, long amountSat, string? message, CancellationToken ct = default)
    {
        var form = new List<KeyValuePair<string, string?>>
        {
            new("offer", offer),
            new("amountSat", amountSat.ToString()),
            new("message", message),
        };
        return PostJson<PaymentResult>("payoffer", form, ct);
    }

    public Task<PaymentResult?> PayLnAddress(string address, long amountSat, string? message, CancellationToken ct = default)
    {
        var form = new List<KeyValuePair<string, string?>>
        {
            new("address", address),
            new("amountSat", amountSat.ToString()),
            new("message", message),
        };
        return PostJson<PaymentResult>("paylnaddress", form, ct);
    }

    // SEND ONCHAIN.. sendtoaddress returns the txid as a bare string
    public Task<string> SendToAddress(string address, long amountSat, int feerateSatByte, CancellationToken ct = default)
    {
        var form = new List<KeyValuePair<string, string?>>
        {
            new("address", address),
            new("amountSat", amountSat.ToString()),
            new("feerateSatByte", feerateSatByte.ToString()),
        };
        return PostString("sendtoaddress", form, ct);
    }

    public Task<DecodedInvoice?> DecodeInvoice(string invoice, CancellationToken ct = default)
        => PostJson<DecodedInvoice>("decodeinvoice",  new[] { new KeyValuePair<string, string?>("invoice", invoice) }, ct);

    public Task<DecodedInvoice?> DecodeOffer(string offer, CancellationToken ct = default)
        => PostJson<DecodedInvoice>("decodeoffer", new[] { new KeyValuePair<string, string?>("offer", offer) }, ct);

    public Task<List<IncomingPayment>?> ListIncomingPayments(bool all, string? externalId, int? limit, int? offset, CancellationToken ct = default)
    {
        var q = BuildQuery(new[]
        {
            new KeyValuePair<string, string?>("all", all ? "true" : null),
            new KeyValuePair<string, string?>("externalId", externalId),
            new KeyValuePair<string, string?>("limit", limit?.ToString()),
            new KeyValuePair<string, string?>("offset", offset?.ToString()),
        });
        return GetJson<List<IncomingPayment>>("payments/incoming", q, ct, preferLimited: true);
    }

    public Task<IncomingPayment?> GetIncomingPayment(string paymentHash, CancellationToken ct = default)
        => GetJson<IncomingPayment>($"payments/incoming/{Uri.EscapeDataString(paymentHash)}", "", ct, preferLimited: true);

    public Task<List<OutgoingPayment>?> ListOutgoingPayments(bool all, int? limit, int? offset, CancellationToken ct = default)
    {
        var q = BuildQuery(new[]
        {
            new KeyValuePair<string, string?>("all", all ? "true" : null),
            new KeyValuePair<string, string?>("limit", limit?.ToString()),
            new KeyValuePair<string, string?>("offset", offset?.ToString()),
        });
        return GetJson<List<OutgoingPayment>>("payments/outgoing", q, ct, preferLimited: true);
    }

    public Task<OutgoingPayment?> GetOutgoingPayment(string paymentId, CancellationToken ct = default)
        => GetJson<OutgoingPayment>($"payments/outgoing/{Uri.EscapeDataString(paymentId)}", "", ct, preferLimited: true);


    public Task<string> CloseChannel(string channelId, string address, int feerateSatByte, CancellationToken ct = default)
    {
        var form = new List<KeyValuePair<string, string?>>
        {
            new("channelId", channelId),
            new("address", address),
            new("feerateSatByte", feerateSatByte.ToString()),
        };
        return PostString("closechannel", form, ct);
    }


    public async Task<(bool ok, string message)> TestConnection(CancellationToken ct = default)
    {
        try
        {
            var info = await GetInfo(ct);
            if (info?.NodeId is null)
                return (false, "Connected, but response did not contain a nodeId.");
            return (true, $"Connected. Node {info.NodeId[..Math.Min(16, info.NodeId.Length)]}… on {info.Chain} at block {info.BlockHeight}.");
        }
        catch (PhoenixdApiException ex)
        {
            return (false, $"phoenixd returned HTTP {ex.StatusCode}: {ex.Body}");
        }
        catch (Exception ex)
        {
            return (false, $"Could not reach phoenixd: {ex.Message}");
        }
    }
}

public class PhoenixdApiException : Exception
{
    public int StatusCode { get; }
    public string Body { get; }
    public PhoenixdApiException(int statusCode, string body)
        : base($"phoenixd API error {statusCode}: {body}")
    {
        StatusCode = statusCode;
        Body = body;
    }
}
