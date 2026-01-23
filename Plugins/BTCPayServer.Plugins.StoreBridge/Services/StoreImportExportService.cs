using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Plugins.StoreBridge.ViewModels;
using BTCPayServer.Services.Stores;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.StoreBridge.Services;

public class StoreImportExportService
{
    private readonly StoreRepository _storeRepository;
    private readonly ApplicationDbContext _dbContext;

    public StoreImportExportService(
        StoreRepository storeRepository,
        ApplicationDbContext dbContext)
    {
        _storeRepository = storeRepository;
        _dbContext = dbContext;
    }


    /// <summary>
    /// Export a complete store configuration
    /// </summary>
    public async Task<StoreExportData> ExportStoreAsync(string storeId)
    {
        var store = await _storeRepository.FindStore(storeId);
        if (store == null)
            throw new InvalidOperationException($"Store {storeId} not found");

        var exportData = new StoreExportData
        {
            ExportedAt = DateTime.UtcNow,
            ExportedFrom = "BTCPay Server",
            Store = await MapStoreDataAsync(store)
        };

        // Export wallets (xpubs only)
        exportData.Wallets = await ExportWalletsAsync(storeId);

        // Export payment methods
        exportData.PaymentMethods = await ExportPaymentMethodsAsync(storeId);

        // Export webhooks
        exportData.Webhooks = await ExportWebhooksAsync(storeId);

        // Export users and roles
        exportData.Users = await ExportStoreUsersAsync(storeId);

        // Export apps
        exportData.Apps = await ExportAppsAsync(storeId);

        return exportData;
    }

    /// <summary>
    /// Import a store configuration
    /// </summary>
    public async Task<StoreImportResult> ImportStoreAsync(
        StoreExportData exportData,
        StoreImportOptions options,
        string currentUserId)
    {
        var result = new StoreImportResult();

        try
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                // Create or update store
                var storeId = await ImportStoreDataAsync(exportData.Store, options, currentUserId);
                result.NewStoreId = storeId;

                // Import wallets
                if (options.ImportWallets && exportData.Wallets.Any())
                {
                    result.Statistics.WalletsImported = await ImportWalletsAsync(storeId, exportData.Wallets);
                }

                // Import payment methods
                if (options.ImportPaymentMethods && exportData.PaymentMethods.Any())
                {
                    result.Statistics.PaymentMethodsImported =
                        await ImportPaymentMethodsAsync(storeId, exportData.PaymentMethods);
                }

                // Import webhooks
                if (options.ImportWebhooks && exportData.Webhooks.Any())
                {
                    result.Statistics.WebhooksImported =
                        await ImportWebhooksAsync(storeId, exportData.Webhooks);
                }

                // Import users (with caution)
                if (options.ImportUsers && exportData.Users.Any())
                {
                    var userResult = await ImportStoreUsersAsync(storeId, exportData.Users);
                    result.Statistics.UsersImported = userResult.imported;
                    result.Warnings.AddRange(userResult.warnings);
                }

                // Import apps
                if (options.ImportApps && exportData.Apps.Any())
                {
                    result.Statistics.AppsImported = await ImportAppsAsync(storeId, exportData.Apps);
                }

                await transaction.CommitAsync();
                result.Success = true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                result.Success = false;
                result.Errors.Add($"Import failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Transaction error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Serialize export data to JSON
    /// </summary>
    public string SerializeExport(StoreExportData exportData)
    {
        return JsonConvert.SerializeObject(exportData, Formatting.Indented, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        });
    }

    /// <summary>
    /// Deserialize import data from JSON
    /// </summary>
    public StoreExportData DeserializeImport(string json)
    {
        var data = JsonConvert.DeserializeObject<StoreExportData>(json);
        if (data == null)
            throw new InvalidOperationException("Failed to deserialize import data");

        ValidateImportData(data);
        return data;
    }

    /// <summary>
    /// Validate import data before processing
    /// </summary>
    private void ValidateImportData(StoreExportData data)
    {
        if (data.Version > 1)
            throw new InvalidOperationException($"Unsupported export version: {data.Version}");

        if (string.IsNullOrEmpty(data.Store.StoreName))
            throw new InvalidOperationException("Store name is required");

        // Validate wallet data doesn't contain private keys
        foreach (var wallet in data.Wallets)
        {
            if (wallet.DerivationScheme.Contains("xprv", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Private keys detected in wallet data. Only xpubs are allowed.");
        }
    }

    // Private helper methods for data mapping

    private async Task<StoreData> MapStoreDataAsync(Data.StoreData store)
    {
        var blob = store.GetStoreBlob();

        return new StoreData
        {
            Id = store.Id,
            StoreName = store.StoreName,
            StoreWebsite = blob.StoreWebsite,
            DefaultCurrency = blob.DefaultCurrency,
            SpeedPolicy = (int)blob.SpeedPolicy,
            NetworkFeeMode = blob.NetworkFeeMode?.ToString(),
            Spread = blob.Spread,
            PayJoinEnabled = blob.PayJoinEnabled,
            AnyoneCanCreateInvoice = blob.AnyoneCanCreateInvoice,
            CustomLogo = blob.CustomLogo,
            CustomCSS = blob.CustomCSS,
            DefaultLang = blob.DefaultLang,
            InvoiceExpiration = blob.InvoiceExpiration.HasValue,
            InvoiceExpirationMinutes = (int)(blob.InvoiceExpiration?.TotalMinutes ?? 15),
            MonitoringExpiration = (int)blob.MonitoringExpiration.TotalMinutes
        };
    }

    private async Task<List<WalletData>> ExportWalletsAsync(string storeId)
    {
        // Implementation would query wallet configurations
        // This is a placeholder - actual implementation depends on BTCPay's wallet storage
        return new List<WalletData>();
    }

    private async Task<List<PaymentMethodData>> ExportPaymentMethodsAsync(string storeId)
    {
        // Implementation would query payment method configurations
        return new List<PaymentMethodData>();
    }

    private async Task<List<WebhookData>> ExportWebhooksAsync(string storeId)
    {
        var webhooks = await _dbContext.Webhooks
            .Where(w => w.StoreId == storeId)
            .ToListAsync();

        return webhooks.Select(w => new WebhookData
        {
            Enabled = w.Enabled,
            AutomaticRedelivery = w.AutomaticRedelivery,
            Url = w.Url,
            AuthorizedEvents = w.GetBlob().AuthorizedEvents.ToList(),
            Secret = w.Secret
        }).ToList();
    }

    private async Task<List<StoreUserData>> ExportStoreUsersAsync(string storeId)
    {
        var storeUsers = await _dbContext.UserStore
            .Include(us => us.ApplicationUser)
            .Where(us => us.StoreDataId == storeId)
            .ToListAsync();

        return storeUsers.Select(us => new StoreUserData
        {
            Email = us.ApplicationUser.Email ?? string.Empty,
            Role = us.Role
        }).ToList();
    }

    private async Task<List<AppData>> ExportAppsAsync(string storeId)
    {
        var apps = await _dbContext.Apps
            .Where(a => a.StoreDataId == storeId)
            .ToListAsync();

        return apps.Select(a => new AppData
        {
            AppType = a.AppType,
            AppName = a.Name,
            Settings = JsonConvert.DeserializeObject<Dictionary<string, object>>(a.Settings)
                ?? new Dictionary<string, object>()
        }).ToList();
    }

    private async Task<string> ImportStoreDataAsync(
        StoreData storeData,
        StoreImportOptions options,
        string currentUserId)
    {
        var store = new Data.StoreData
        {
            Id = options.NewStoreId ?? Guid.NewGuid().ToString(),
            StoreName = options.NewStoreName ?? storeData.StoreName
        };

        var blob = store.GetStoreBlob();
        blob.StoreWebsite = storeData.StoreWebsite;
        blob.DefaultCurrency = storeData.DefaultCurrency;
        blob.PayJoinEnabled = storeData.PayJoinEnabled;
        blob.AnyoneCanCreateInvoice = storeData.AnyoneCanCreateInvoice;
        blob.CustomLogo = storeData.CustomLogo;
        blob.CustomCSS = storeData.CustomCSS;
        blob.DefaultLang = storeData.DefaultLang;

        if (storeData.InvoiceExpiration)
            blob.InvoiceExpiration = TimeSpan.FromMinutes(storeData.InvoiceExpirationMinutes);

        blob.MonitoringExpiration = TimeSpan.FromMinutes(storeData.MonitoringExpiration);

        store.SetStoreBlob(blob);

        await _storeRepository.CreateStore(currentUserId, store);
        return store.Id;
    }

    private async Task<int> ImportWalletsAsync(string storeId, List<WalletData> wallets)
    {
        // Implementation for importing wallet configurations
        // This would use BTCPay's wallet derivation scheme setup
        return 0;
    }

    private async Task<int> ImportPaymentMethodsAsync(string storeId, List<PaymentMethodData> paymentMethods)
    {
        // Implementation for importing payment method configurations
        return 0;
    }

    private async Task<int> ImportWebhooksAsync(string storeId, List<WebhookData> webhooks)
    {
        int count = 0;
        foreach (var webhookData in webhooks)
        {
            var webhook = new WebhookData
            {
                StoreId = storeId,
                Enabled = webhookData.Enabled,
                AutomaticRedelivery = webhookData.AutomaticRedelivery,
                Url = webhookData.Url,
                Secret = webhookData.Secret
            };

            var blob = new WebhookBlob
            {
                AuthorizedEvents = webhookData.AuthorizedEvents.ToHashSet()
            };

            webhook.SetBlob(blob);

            _dbContext.Webhooks.Add(webhook);
            count++;
        }

        await _dbContext.SaveChangesAsync();
        return count;
    }

    private async Task<(int imported, List<string> warnings)> ImportStoreUsersAsync(
        string storeId,
        List<StoreUserData> users)
    {
        var warnings = new List<string>();
        int imported = 0;

        foreach (var userData in users)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == userData.Email);
            if (user == null)
            {
                warnings.Add($"User {userData.Email} not found on this server - skipped");
                continue;
            }

            var existingUserStore = await _dbContext.UserStore
                .FirstOrDefaultAsync(us => us.StoreDataId == storeId && us.ApplicationUserId == user.Id);

            if (existingUserStore == null)
            {
                _dbContext.UserStore.Add(new UserStore
                {
                    StoreDataId = storeId,
                    ApplicationUserId = user.Id,
                    Role = userData.Role
                });
                imported++;
            }
        }

        await _dbContext.SaveChangesAsync();
        return (imported, warnings);
    }

    private async Task<int> ImportAppsAsync(string storeId, List<AppData> apps)
    {
        int count = 0;
        foreach (var appData in apps)
        {
            var app = new AppData
            {
                Id = Guid.NewGuid().ToString(),
                StoreDataId = storeId,
                AppType = appData.AppType,
                Name = appData.AppName,
                Settings = JsonConvert.SerializeObject(appData.Settings),
                Created = DateTime.UtcNow
            };

            _dbContext.Apps.Add(app);
            count++;
        }

        await _dbContext.SaveChangesAsync();
        return count;
    }
}
