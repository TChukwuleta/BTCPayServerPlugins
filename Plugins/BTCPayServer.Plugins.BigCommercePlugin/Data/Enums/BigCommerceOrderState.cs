namespace BTCPayServer.Plugins.BigCommercePlugin.Data;

public enum BigCommerceOrderState
{
    INCOMPLETE = 0,
    PENDING = 1,
    REFUNDED = 4,
    CANCELLED = 5,
    DECLINED = 6,
    AWAITING_PAYMENT = 7,
    COMPLETED = 10,
    AWAITING_FULFILLMENT = 11,
    MANUAL_VERIFICATION_REQUIRED = 12,
    DISPUTED = 13
}
 