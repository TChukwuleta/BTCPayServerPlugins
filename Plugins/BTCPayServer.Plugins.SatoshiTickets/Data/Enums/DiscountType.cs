namespace BTCPayServer.Plugins.SatoshiTickets.Data;

public enum DiscountType
{
    Percentage = 1,
    FixedAmount
}

public enum DiscountValidationStatus
{
    Valid,
    NotFound,
    Inactive,
    Expired,
    MaxUsesReached,
    NotApplicableToCart,
    MinQuantityNotMet
}