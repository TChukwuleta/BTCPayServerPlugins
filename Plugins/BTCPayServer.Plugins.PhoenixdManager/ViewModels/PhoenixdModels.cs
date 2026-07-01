using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTCPayServer.Plugins.PhoenixdManager.ViewModels;

public class PhoenixdSettings
{
    // e.g. http://phoenixd:9740  (no trailing slash needed)
    public string ServerUrl { get; set; } = "http://127.0.0.1:9740";
    // full-access http-password from phoenix.conf
    public string Password { get; set; } = "";
    // optional: limited-access password, used for read-only calls if provided
    public string? LimitedAccessPassword { get; set; }
}

public class NodeInfo
{
    [JsonPropertyName("nodeId")] public string? NodeId { get; set; }
    [JsonPropertyName("chain")] public string? Chain { get; set; }
    [JsonPropertyName("blockHeight")] public long BlockHeight { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("channels")] public List<ChannelInfo>? Channels { get; set; }
}

public class BalanceInfo
{
    [JsonPropertyName("balanceSat")] public long BalanceSat { get; set; }
    [JsonPropertyName("feeCreditSat")] public long FeeCreditSat { get; set; }
}

public class ChannelInfo
{
    [JsonPropertyName("channelId")] public string? ChannelId { get; set; }
    [JsonPropertyName("state")] public string? State { get; set; }
    [JsonPropertyName("balanceSat")] public long BalanceSat { get; set; }
    [JsonPropertyName("inboundLiquiditySat")] public long InboundLiquiditySat { get; set; }
    [JsonPropertyName("capacitySat")] public long CapacitySat { get; set; }
    [JsonPropertyName("fundingTxId")] public string? FundingTxId { get; set; }
}

public class OfferInfo
{
    // getoffer returns the raw BOLT12 offer string as the body; wrapped for the view.
    public string? Offer { get; set; }
}

public class LnAddressInfo
{
    public string? LnAddress { get; set; }
}

public class CreatedInvoice
{
    [JsonPropertyName("amountSat")] public long AmountSat { get; set; }
    [JsonPropertyName("paymentHash")] public string? PaymentHash { get; set; }
    [JsonPropertyName("serialized")] public string? Serialized { get; set; }
}

public class DecodedInvoice
{
    [JsonPropertyName("chain")] public string? Chain { get; set; }
    [JsonPropertyName("amount")] public long? Amount { get; set; }
    [JsonPropertyName("paymentHash")] public string? PaymentHash { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("expirySeconds")] public long? ExpirySeconds { get; set; }
    [JsonPropertyName("timestampSeconds")] public long? TimestampSeconds { get; set; }
}

public class PaymentResult
{
    [JsonPropertyName("recipientAmountSat")] public long RecipientAmountSat { get; set; }
    [JsonPropertyName("routingFeeSat")] public long RoutingFeeSat { get; set; }
    [JsonPropertyName("paymentId")] public string? PaymentId { get; set; }
    [JsonPropertyName("paymentHash")] public string? PaymentHash { get; set; }
    [JsonPropertyName("paymentPreimage")] public string? PaymentPreimage { get; set; }
}

public class OnChainSendResult
{
    // sendtoaddress returns the txid as the body; wrapped for the view.
    public string? TxId { get; set; }
}

public class LiquidityFeeEstimate
{
    [JsonPropertyName("miningFeeSat")] public long MiningFeeSat { get; set; }
    [JsonPropertyName("serviceFeeSat")] public long ServiceFeeSat { get; set; }
}

public class IncomingPayment
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("subType")] public string? SubType { get; set; }
    [JsonPropertyName("paymentHash")] public string? PaymentHash { get; set; }
    [JsonPropertyName("preimage")] public string? Preimage { get; set; }
    [JsonPropertyName("externalId")] public string? ExternalId { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("invoice")] public string? Invoice { get; set; }
    [JsonPropertyName("isPaid")] public bool IsPaid { get; set; }
    [JsonPropertyName("isExpired")] public bool IsExpired { get; set; }
    [JsonPropertyName("receivedSat")] public long ReceivedSat { get; set; }
    [JsonPropertyName("fees")] public long Fees { get; set; }
    [JsonPropertyName("completedAt")] public long? CompletedAt { get; set; }
    [JsonPropertyName("createdAt")] public long CreatedAt { get; set; }
}

public class OutgoingPayment
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("subType")] public string? SubType { get; set; }
    [JsonPropertyName("paymentId")] public string? PaymentId { get; set; }
    [JsonPropertyName("paymentHash")] public string? PaymentHash { get; set; }
    [JsonPropertyName("preimage")] public string? Preimage { get; set; }
    [JsonPropertyName("isPaid")] public bool IsPaid { get; set; }
    [JsonPropertyName("sent")] public long Sent { get; set; }
    [JsonPropertyName("fees")] public long Fees { get; set; }
    [JsonPropertyName("invoice")] public string? Invoice { get; set; }
    [JsonPropertyName("completedAt")] public long? CompletedAt { get; set; }
    [JsonPropertyName("createdAt")] public long CreatedAt { get; set; }
}

public class DashboardViewModel
{
    public bool Configured { get; set; }
    public string? Error { get; set; }
    public string? ServerUrl { get; set; }
    public NodeInfo? NodeInfo { get; set; }
    public BalanceInfo? Balance { get; set; }
    public List<ChannelInfo> Channels { get; set; } = new();
    public string? Offer { get; set; }
    public string? LnAddress { get; set; }
    public List<IncomingPayment> RecentIncoming { get; set; } = new();
    public List<OutgoingPayment> RecentOutgoing { get; set; } = new();
}

public class ApiActionResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? RawJson { get; set; }
}
