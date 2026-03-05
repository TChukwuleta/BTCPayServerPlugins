using BTCPayServer.Abstractions.Contracts;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.SquareUp.Services;

public class SquareService(
    SquareClientFactory clientFactory,
    IStoreRepository storeRepository,
    ILogger<SquareService> logger)
{
}
