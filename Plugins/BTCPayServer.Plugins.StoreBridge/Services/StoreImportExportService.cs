using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Data;
using BTCPayServer.Plugins.StoreBridge.ViewModels;
using BTCPayServer.Services;
using BTCPayServer.Services.Apps;
using BTCPayServer.Services.Stores;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.StoreBridge.Services;

public class StoreImportExportService
{
    private readonly AppService _appService;
    private readonly ApplicationDbContext _dbContext;
    private readonly StoreRepository _storeRepository;
    private readonly SettingsRepository _settingsRepository;

    public StoreImportExportService(ApplicationDbContext dbContext,
        StoreRepository storeRepository, SettingsRepository settingsRepository, AppService appService)
    {
        _dbContext = dbContext;
        _appService = appService;
        _storeRepository = storeRepository;
        _settingsRepository = settingsRepository;
    }

    public async Task<StoreExportData> ExportStore(string sourceInstanceUrl, string userId, StoreData store, List<string> selectedOptions)
    {
        // Settings... Branding, Payment
        // Forms
        var originalBlob = store.GetStoreBlob();
        var blob = DefaultStoreBlobSettings(originalBlob);

        var exportData = new StoreExportData
        {
            Version = 1,
            ExportDate = DateTime.UtcNow,
            ExportedFrom = sourceInstanceUrl,
            Store = new StoreBridgeData
            {
                Id = store.Id,
                Spread = blob.Spread,
                StoreName = store.StoreName,
                DefaultLang = blob.DefaultLang,
                StoreWebsite = store.StoreWebsite,
                DefaultCurrency = blob.DefaultCurrency,
                SpeedPolicy = store.SpeedPolicy.ToString(),
                StoreBlob = JsonConvert.SerializeObject(blob),
                DerivationStrategies = store.DerivationStrategies
            }
        };

        if (selectedOptions.Contains("EmailSettings"))
        {
            blob.PrimaryRateSettings = originalBlob.PrimaryRateSettings;
            blob.FallbackRateSettings = originalBlob.FallbackRateSettings;
            exportData.Store ??= new();
            exportData.Store.StoreBlob = JsonConvert.SerializeObject(blob);
        }
        if (selectedOptions.Contains("RateSettings"))
        {
            blob.EmailSettings = originalBlob.EmailSettings;
            exportData.Store ??= new();
            exportData.Store.StoreBlob = JsonConvert.SerializeObject(blob);
        }
        if (selectedOptions.Contains("CheckoutSettings"))
        {
            blob.ShowPayInWalletButton = originalBlob.ShowPayInWalletButton;
            blob.ShowStoreHeader = originalBlob.ShowStoreHeader;
            blob.CelebratePayment = originalBlob.CelebratePayment;
            blob.PlaySoundOnPayment = originalBlob.PlaySoundOnPayment;
            blob.OnChainWithLnInvoiceFallback = originalBlob.OnChainWithLnInvoiceFallback;
            blob.LightningAmountInSatoshi = originalBlob.LightningAmountInSatoshi;
            blob.LazyPaymentMethods = originalBlob.LazyPaymentMethods;
            blob.RedirectAutomatically = originalBlob.RedirectAutomatically;
            blob.ReceiptOptions = originalBlob.ReceiptOptions;
            blob.HtmlTitle = originalBlob.HtmlTitle;
            blob.StoreSupportUrl = originalBlob.StoreSupportUrl;
            blob.DisplayExpirationTimer = originalBlob.DisplayExpirationTimer;
            blob.AutoDetectLanguage = originalBlob.AutoDetectLanguage;
            blob.DefaultLang = originalBlob.DefaultLang;
            exportData.Store ??= new();
            exportData.Store.StoreBlob = JsonConvert.SerializeObject(blob);
        }
        if (selectedOptions.Contains("Webhooks"))
        {
            var webhooks = await _storeRepository.GetWebhooks(store.Id);
            exportData.Webhooks = webhooks.Select(wh => new WebhookExport
            {
                Blob2Json = wh.Blob2,
                BlobJson = JsonConvert.SerializeObject(wh.GetBlob())
            }).ToList();
        }
        if (selectedOptions.Contains("Roles"))
        {
            var roles = await _storeRepository.GetStoreRoles(store.Id);
            exportData.Roles = roles.Select(r => new RoleExport
            {
                Role = r.Role,
                Permissions = r.Permissions?.ToList()
            }).ToList();
        }


        // Export Checkout Settings
        if (selectedOptions.Contains("CheckoutSettings"))
        {
            exportData.CheckoutSettings = new CheckoutSettingsExport
            {
                SpeedPolicy = store.SpeedPolicy.ToString(),
                CheckoutType = blob.CheckoutType?.ToString(),
                DefaultPaymentMethod = blob.DefaultPaymentMethod,
                LazyPaymentMethods = blob.LazyPaymentMethods,
                RedirectAutomatically = blob.RedirectAutomatically,
                ShowRecommendedFee = blob.ShowRecommendedFee,
                RecommendedFeeBlockTarget = blob.RecommendedFeeBlockTarget,
                DisplayExpirationTimer = blob.DisplayExpirationTimer,
                RequiresRefundEmail = blob.RequiresRefundEmail,
                CheckoutFormId = blob.CheckoutFormId
            };
        }




        if (selectedOptions.Contains("PaymentMethods"))
        {
            var paymentMethodConfig = store.GetPaymentMethodConfigs(true);

            paymentMethodConfig.Select(pm => new PaymentMethodExport
            {
                PaymentMethodId = pm.Key.ToString(),
                ConfigJson = JsonConvert.SerializeObject(pm.Value)
            }).ToList();
        }

        if (selectedOptions.Contains("Apps"))
        {
            var apps = await _appService.GetAllApps(userId: userId, storeId: store.Id);
            exportData.Apps = apps.Select(app => new AppExport
            {
                AppId = app.Id,
                AppName = app.AppName,
                AppType = app.AppType,
                Created = app.Created,
                SettingsJson = JsonConvert.SerializeObject(app.App)
            }).ToList();
        }



        // Create encrypted export file
        var encryptedData = _exportService.CreateExport(exportData, store.Id, compress: true);
        var filename = $"btcpay-store-{store.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}.btcpayexport";

        return File(encryptedData, "application/octet-stream", filename);

        return exportData;
    }


    private StoreBlob DefaultStoreBlobSettings(StoreBlob blob)
    {
        blob.EmailSettings = null;
        blob.PrimaryRateSettings = null;
        blob.FallbackRateSettings = null;
        blob.ShowPayInWalletButton = false;
        blob.ShowStoreHeader = false;
        blob.CelebratePayment = false;
        blob.PlaySoundOnPayment = false;
        blob.OnChainWithLnInvoiceFallback = false;
        blob.LightningAmountInSatoshi = false;
        blob.LazyPaymentMethods = false;
        blob.RedirectAutomatically = false;
        blob.ReceiptOptions = null;
        blob.HtmlTitle = string.Empty;
        blob.StoreSupportUrl = string.Empty;
        blob.AutoDetectLanguage = false;
        blob.DefaultLang = string.Empty;
        return blob;
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

        /*try
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
        }*/

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
        /*foreach (var wallet in data.Wallets)
        {
            if (wallet.DerivationScheme.Contains("xprv", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Private keys detected in wallet data. Only xpubs are allowed.");
        }*/
    }

    private StoreBridgeData MapStoreData(StoreData store)
    {
        var blob = store.GetStoreBlob();
        return new StoreBridgeData
        {
            Id = store.Id,
            StoreName = store.StoreName,
            StoreBlob = store.StoreBlob,
            SpeedPolicy = store.SpeedPolicy.ToString(),
            DerivationStrategies = store.DerivationStrategies,
            StoreWebsite = store.StoreWebsite,
            DefaultCurrency = blob.DefaultCurrency,
            Spread = blob.Spread,
            PayJoinEnabled = blob.PayJoinEnabled,
            DefaultLang = blob.DefaultLang
        };
    }


    /*private async Task<List<WebhookData>> ExportWebhooksAsync(string storeId)
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
    }*/

    /*private async Task<string> ImportStoreDataAsync(
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
    }*/
}
