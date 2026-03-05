using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using BTCPayServer.Plugins.SquareUp.Data;
using NBitcoin;

namespace BTCPayServer.Plugins.SquareUp.Services;

public class SquareClientFactory
{
    private readonly Dictionary<string, HttpClient> _clients = new();
    private readonly Lock _lock = new();

    private const string ProductionBaseUrl = "https://connect.squareup.com";
    private const string SandboxBaseUrl = "https://connect.squareupsandbox.com";

    /// <summary>
    /// Returns a configured Square client for the given store settings.
    /// Clients are cached by store ID to avoid re-creating them on every request.
    /// </summary>
    public SquareClient GetClient(string storeId, SquareUpStoreSetting settings)
    {
        var cacheKey = $"{storeId}:{settings.AccessToken}";

        lock (_lock)
        {
            if (_clients.TryGetValue(cacheKey, out var existing))
                return existing;

            var environment = settings.IsSandbox
                ? Square.Environment.Sandbox
                : Square.Environment.Production;

            var client = new SquareClient.Builder()
                .AccessToken(settings.AccessToken)
                .Environment(environment)
                .Build();

            _clients[cacheKey] = client;
            return client;
        }
    }

    /// <summary>
    /// Invalidates the cached client for a store (call after settings change).
    /// </summary>
    public void InvalidateClient(string storeId)
    {
        lock (_lock)
        {
            var keysToRemove = _clients.Keys
                .Where(k => k.StartsWith($"{storeId}:"))
                .ToList();
            foreach (var key in keysToRemove)
                _clients.Remove(key);
        }
    }
