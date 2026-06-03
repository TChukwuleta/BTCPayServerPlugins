using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.Data.Subscriptions;
using BTCPayServer.Forms;
using BTCPayServer.Plugins.StoreBridge.ViewModels;
using BTCPayServer.Plugins.Subscriptions;
using BTCPayServer.Services.Apps;
using BTCPayServer.Services.Stores;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.StoreBridge.Services;

public class StoreImportExportService(StoreRepository storeRepository, AppService appService, FormDataService formDataService, ApplicationDbContextFactory dbContext)
{
    private const string MAGIC_HEADER = "BTCPAY_STOREBRIDGE";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<StoreExportData> GetExportData(string sourceInstanceUrl, string userId,
        BTCPayServer.Data.StoreData store, List<string> selectedOptions)
    {
        var originalBlob = store.GetStoreBlob();
        var blob = DefaultStoreBlobSettings(originalBlob);
        var exportData = new StoreExportData
        {
            Version = 1,
            ExportDate = DateTime.UtcNow,
            ExportedFrom = sourceInstanceUrl,
            SelectedOptions = JsonConvert.SerializeObject(selectedOptions),
            Store = new StoreBridgeData
            {
                StoreId = store.Id,
                StoreName = store.StoreName,
                SpeedPolicy = store.SpeedPolicy.ToString(),
                DerivationStrategies = store.DerivationStrategies
            }
        };

        if (selectedOptions.Contains("BrandingSettings"))
        {
            blob.LogoUrl = originalBlob.LogoUrl;
            blob.CssUrl = originalBlob.CssUrl;
            blob.BrandColor = originalBlob.BrandColor;
            blob.ApplyBrandColorToBackend = originalBlob.ApplyBrandColorToBackend;
        }
        if (selectedOptions.Contains("EmailSettings"))
            blob.EmailSettings = originalBlob.EmailSettings;

        if (selectedOptions.Contains("RateSettings"))
        {
            blob.PrimaryRateSettings = originalBlob.PrimaryRateSettings;
            blob.FallbackRateSettings = originalBlob.FallbackRateSettings;
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
        }

        exportData.Store.StoreBlob = JsonConvert.SerializeObject(blob);
        await using var ctx = dbContext.CreateContext();

        if (selectedOptions.Contains("PullPayments"))
        {
            var pullPayments = await ctx.PullPayments
                .Include(pp => pp.Payouts)
                .Where(pp => pp.StoreId == store.Id && !pp.Archived)
                .ToListAsync();

            exportData.PullPayments = pullPayments.Select(pp =>
            {
                var ppBlob = pp.GetBlob();
                return new PullPaymentExport
                {
                    Name = ppBlob.Name,
                    Description = ppBlob.Description,
                    Amount = pp.Limit,
                    Currency = pp.Currency,
                    StartsAt = pp.StartDate,
                    ExpiresAt = pp.EndDate,
                    AutoApproveClaims = ppBlob.AutoApproveClaims,
                    Payouts = pp.Payouts?.Select(p =>
                    {
                        return new PayoutExport
                        {
                            Amount = p.OriginalAmount,
                            Currency = p.OriginalCurrency,
                            State = p.State.ToString(),
                            CreatedAt = p.Date
                        };
                    }).ToList()
                };
            }).ToList();
        }

        if (selectedOptions.Contains("StoreUsers"))
        {
            var storeUsers = await storeRepository.GetStoreUsers(store.Id);
            exportData.StoreUsers = storeUsers.Select(u => new StoreUserExport
            {
                Email = u.Email,
                Role = u.StoreRole?.Role
            }).ToList();
        }

        if (selectedOptions.Contains("Subscriptions"))
        {
            var offerings = new List<SubscriptionOfferingExportData>();
            var offeringDataList = await ctx.Offerings
                .Include(o => o.App).Include(o => o.Features).Include(o => o.Plans)
                .Where(o => o.App.StoreDataId == store.Id).ToListAsync();

            foreach (var offering in offeringDataList)
            {
                var offeringExport = new SubscriptionOfferingExportData
                {
                    AppName = offering.App.Name,
                    SuccessRedirectUrl = offering.SuccessRedirectUrl,
                    DefaultPaymentRemindersDays = offering.DefaultPaymentRemindersDays,
                    Metadata = offering.Metadata,
                    Features = offering.Features?
                        .Select(f => new OfferingFeatureData
                        {
                            CustomId = f.CustomId,
                            Description = f.Description
                        }).ToList(),
                    Plans = new List<SubscriptionPlanExportData>()
                };
                await ctx.Plans.FetchPlanFeaturesAsync(offering.Plans.ToArray());
                foreach (var plan in offering.Plans)
                {
                    offeringExport.Plans.Add(new SubscriptionPlanExportData
                    {
                        Name = plan.Name,
                        Description = plan.Description,
                        Price = plan.Price,
                        Currency = plan.Currency,
                        RecurringType = plan.RecurringType.ToString(),
                        TrialDays = plan.TrialDays,
                        GracePeriodDays = plan.GracePeriodDays,
                        OptimisticActivation = plan.OptimisticActivation,
                        Renewable = plan.Renewable,
                        Metadata = plan.Metadata,
                        FeatureIds = plan.PlanFeatures?.Select(f => f.Feature.CustomId).ToList()
                    });
                }
                offerings.Add(offeringExport);
            }
            exportData.SubscriptionOfferings = offerings;
        }

        if (selectedOptions.Contains("Webhooks"))
        {
            var webhooks = await storeRepository.GetWebhooks(store.Id);
            exportData.Webhooks = webhooks.Select(wh => new WebhookExport
            {
                Blob2Json = wh.Blob2,
                BlobJson = JsonConvert.SerializeObject(wh.GetBlob())
            }).ToList();
        }

        if (selectedOptions.Contains("Roles"))
        {
            var roles = await storeRepository.GetStoreRoles(store.Id);
            exportData.Roles = roles.Select(r => new RoleExport
            {
                Role = r.Role,
                Permissions = r.Permissions?.ToList()
            }).ToList();
        }

        if (selectedOptions.Contains("Forms"))
        {
            var forms = await formDataService.GetForms(store.Id);
            exportData.Forms = forms.Select(c => new FormExport
            {
                Public = c.Public,
                Name = c.Name,
                Config = c.Config
            }).ToList();
        }

        if (selectedOptions.Contains("Apps"))
        {
            var apps = await appService.GetAllApps(userId: userId, storeId: store.Id);
            exportData.Apps = apps.Select(app => new AppExport
            {
                AppId = app.Id,
                AppName = app.AppName,
                AppType = app.AppType,
                SettingsJson = JsonConvert.SerializeObject(app.App)
            }).ToList();
        }

        return exportData;
    }

    public async Task<byte[]> ExportStore(string sourceInstanceUrl, string userId, BTCPayServer.Data.StoreData store, List<string> selectedOptions)
    {
        var exportData = await GetExportData(sourceInstanceUrl, userId, store, selectedOptions);
        return CreateExport(exportData, store.Id);
    }

    public async Task<(bool Success, string Message)> ImportStore(BTCPayServer.Data.StoreData destinationStore, byte[] encryptedData, string userId,
        List<string> userSelectedOptions = null)
    {
        var succeeded = new List<string>();
        var failed = new List<(string Section, string Error)>();

        StoreExportData exportData;
        try
        {
            exportData = ParseExport(encryptedData);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to parse export file: {ex.Message}");
        }

        var exportedOptions = !string.IsNullOrEmpty(exportData.SelectedOptions)
            ? JsonConvert.DeserializeObject<List<string>>(exportData.SelectedOptions) ?? new List<string>()
            : new List<string>();

        var optionsToImport = userSelectedOptions != null ? userSelectedOptions.Intersect(exportedOptions).ToList() : exportedOptions;
        if (!optionsToImport.Any())
            return (false, "No valid options selected for import");

        bool storeModified = false;
        var destinationStoreBlob = destinationStore.GetStoreBlob();
        if (exportData.Store != null && !string.IsNullOrEmpty(exportData.Store.StoreBlob))
        {
            StoreBlob? importedBlob = null;
            try
            {
                importedBlob = JsonConvert.DeserializeObject<StoreBlob>(exportData.Store.StoreBlob);
            }
            catch (Exception ex){ failed.Add(("StoreBlob", ex.Message)); }

            if (importedBlob != null)
            {
                TryImportSection("BrandingSettings", optionsToImport, succeeded, failed, () =>
                {
                    destinationStoreBlob.LogoUrl = importedBlob.LogoUrl;
                    destinationStoreBlob.CssUrl = importedBlob.CssUrl;
                    destinationStoreBlob.BrandColor = importedBlob.BrandColor;
                    destinationStoreBlob.ApplyBrandColorToBackend = importedBlob.ApplyBrandColorToBackend;
                    storeModified = true;
                });

                TryImportSection("EmailSettings", optionsToImport, succeeded, failed, () =>
                {
                    destinationStoreBlob.EmailSettings = importedBlob.EmailSettings;
                    storeModified = true;
                });

                TryImportSection("RateSettings", optionsToImport, succeeded, failed, () =>
                {
                    destinationStoreBlob.PrimaryRateSettings = importedBlob.PrimaryRateSettings;
                    destinationStoreBlob.FallbackRateSettings = importedBlob.FallbackRateSettings;
                    if (Enum.TryParse<SpeedPolicy>(exportData.Store.SpeedPolicy, out var speedPolicy))
                        destinationStore.SpeedPolicy = speedPolicy;

                    storeModified = true;
                });

                TryImportSection("CheckoutSettings", optionsToImport, succeeded, failed, () =>
                {
                    destinationStoreBlob.ShowPayInWalletButton = importedBlob.ShowPayInWalletButton;
                    destinationStoreBlob.ShowStoreHeader = importedBlob.ShowStoreHeader;
                    destinationStoreBlob.CelebratePayment = importedBlob.CelebratePayment;
                    destinationStoreBlob.PlaySoundOnPayment = importedBlob.PlaySoundOnPayment;
                    destinationStoreBlob.OnChainWithLnInvoiceFallback = importedBlob.OnChainWithLnInvoiceFallback;
                    destinationStoreBlob.LightningAmountInSatoshi = importedBlob.LightningAmountInSatoshi;
                    destinationStoreBlob.LazyPaymentMethods = importedBlob.LazyPaymentMethods;
                    destinationStoreBlob.RedirectAutomatically = importedBlob.RedirectAutomatically;
                    destinationStoreBlob.ReceiptOptions = importedBlob.ReceiptOptions;
                    destinationStoreBlob.HtmlTitle = importedBlob.HtmlTitle;
                    destinationStoreBlob.StoreSupportUrl = importedBlob.StoreSupportUrl;
                    destinationStoreBlob.DisplayExpirationTimer = importedBlob.DisplayExpirationTimer;
                    destinationStoreBlob.AutoDetectLanguage = importedBlob.AutoDetectLanguage;
                    destinationStoreBlob.DefaultLang = importedBlob.DefaultLang;
                    storeModified = true;
                });
            }
        }

        if (storeModified)
            destinationStore.SetStoreBlob(destinationStoreBlob);

        if (optionsToImport.Contains("Webhooks") && exportData.Webhooks?.Any() == true)
        {
            var webhookErrors = new List<string>();
            foreach (var webhookExport in exportData.Webhooks.Where(c => !string.IsNullOrEmpty(c.BlobJson)))
            {
                try
                {
                    var webhookBlob = JsonConvert.DeserializeObject<WebhookBlob>(webhookExport.BlobJson);
                    await storeRepository.CreateWebhook(destinationStore.Id, webhookBlob);
                }
                catch (Exception ex) { webhookErrors.Add(ex.Message); }
            }

            if (webhookErrors.Any())
                failed.Add(("Webhooks", string.Join("; ", webhookErrors)));
            else
                succeeded.Add("Webhooks");
        }

        if (optionsToImport.Contains("Roles") && exportData.Roles?.Any() == true)
        {
            var roleErrors = new List<string>();
            foreach (var roleExport in exportData.Roles)
            {
                try
                {
                    var roleId = new StoreRoleId(destinationStore.Id, roleExport.Role);
                    await storeRepository.AddOrUpdateStoreRole(roleId, roleExport.Permissions);
                }
                catch (Exception ex) { roleErrors.Add(ex.Message); }
            }

            if (roleErrors.Any())
                failed.Add(("Roles", string.Join("; ", roleErrors)));
            else
                succeeded.Add("Roles");
        }

        if (optionsToImport.Contains("Forms") && exportData.Forms?.Any() == true)
        {
            var formErrors = new List<string>();
            foreach (var formExport in exportData.Forms)
            {
                try
                {
                    await formDataService.AddOrUpdateForm(new FormData
                    {
                        StoreId = destinationStore.Id,
                        Name = formExport.Name,
                        Config = formExport.Config,
                        Public = formExport.Public
                    });
                }
                catch (Exception ex) { formErrors.Add(ex.Message); }
            }

            if (formErrors.Any())
                failed.Add(("Forms", string.Join("; ", formErrors)));
            else
                succeeded.Add("Forms");
        }

        if (optionsToImport.Contains("Apps") && exportData.Apps?.Any() == true)
        {
            var appErrors = new List<string>();
            foreach (var appExport in exportData.Apps)
            {
                try
                {
                    var appData = JsonConvert.DeserializeObject<AppData>(appExport.SettingsJson);
                    appData.StoreDataId = destinationStore.Id;
                    appData.Name = appExport.AppName;
                    appData.AppType = appExport.AppType;
                    appData.Id = null;
                    appData.Created = DateTime.UtcNow;
                    await appService.UpdateOrCreateApp(appData);
                }
                catch (Exception ex) { appErrors.Add(ex.Message); }
            }

            if (appErrors.Any())
                failed.Add(("Apps", string.Join("; ", appErrors)));
            else
                succeeded.Add("Apps");
        }

        if (optionsToImport.Contains("Subscriptions") && exportData.SubscriptionOfferings?.Any() == true)
        {
            var subErrors = new List<string>();
            await using var ctx = dbContext.CreateContext();
            foreach (var offeringExport in exportData.SubscriptionOfferings)
            {
                try
                {
                    var offering = await appService.CreateOffering(destinationStore.Id, offeringExport.AppName);
                    var offeringData = await ctx.Offerings
                        .Include(o => o.Features)
                        .Include(o => o.Plans)
                        .FirstOrDefaultAsync(o => o.Id == offering.OfferingId);

                    if (offeringData == null) continue;

                    offeringData.SuccessRedirectUrl = offeringExport.SuccessRedirectUrl;
                    offeringData.Metadata = offeringExport.Metadata;
                    offeringData.DefaultPaymentRemindersDays = offeringExport.DefaultPaymentRemindersDays;
                    await ctx.SaveChangesAsync();

                    if (offeringExport.Features?.Any() == true)
                    {
                        foreach (var featureExport in offeringExport.Features)
                        {
                            ctx.Features.Add(new FeatureData
                            {
                                OfferingId = offeringData.Id,
                                CustomId = featureExport.CustomId,
                                Description = featureExport.Description
                            });
                        }
                        await ctx.SaveChangesAsync();
                    }

                    await ctx.Entry(offeringData).Collection(o => o.Features).LoadAsync();

                    if (offeringExport.Plans?.Any() == true)
                    {
                        foreach (var planExport in offeringExport.Plans)
                        {
                            var planData = new PlanData
                            {
                                OfferingId = offeringData.Id,
                                Name = planExport.Name,
                                Description = planExport.Description,
                                Price = planExport.Price,
                                Currency = planExport.Currency,
                                RecurringType = Enum.Parse<PlanData.RecurringInterval>(planExport.RecurringType),
                                Status = Enum.Parse<PlanData.PlanStatus>(planExport.Status ?? "Active"),
                                TrialDays = planExport.TrialDays,
                                GracePeriodDays = planExport.GracePeriodDays,
                                OptimisticActivation = planExport.OptimisticActivation,
                                Renewable = planExport.Renewable,
                                Metadata = planExport.Metadata,
                                MemberCount = 0,
                                MonthlyRevenue = 0m
                            };
                            ctx.Plans.Add(planData);
                            await ctx.SaveChangesAsync();

                            if (planExport.FeatureIds?.Any() == true)
                            {
                                foreach (var featureCustomId in planExport.FeatureIds)
                                {
                                    var feature = offeringData.Features.FirstOrDefault(f => f.CustomId == featureCustomId);
                                    if (feature != null)
                                    {
                                        ctx.PlanFeatures.Add(new PlanFeatureData
                                        {
                                            PlanId = planData.Id,
                                            FeatureId = feature.Id
                                        });
                                    }
                                }
                                await ctx.SaveChangesAsync();
                            }
                        }
                    }
                }
                catch (Exception ex) { subErrors.Add(ex.Message); }
            }

            if (subErrors.Any())
                failed.Add(("Subscriptions", string.Join("; ", subErrors)));
            else
                succeeded.Add("Subscriptions");
        }

        try
        {
            await storeRepository.UpdateStore(destinationStore);
        }
        catch (Exception ex)
        {
            failed.Add(("StoreUpdate", ex.Message));
        }

        if (!succeeded.Any() && failed.Any())
        {
            var errorSummary = string.Join("; ", failed.Select(f => $"{f.Section}: {f.Error}"));
            return (false, $"Import failed: {errorSummary}");
        }

        var successItems = string.Join(", ", succeeded.Select(o =>
            ImportViewModel.OptionMetadata.TryGetValue(o, out var meta) ? meta.Title : o));

        if (failed.Any())
        {
            var failedItems = string.Join(", ", failed.Select(f => f.Section));
            return (true, $"Partially imported: {successItems}. Failed sections: {failedItems}");
        }

        return (true, $"Successfully imported {succeeded.Count} configuration(s): {successItems}");
    }

    private static void TryImportSection(string key, List<string> optionsToImport, List<string> succeeded,
        List<(string Section, string Error)> failed, Action action)
    {
        if (!optionsToImport.Contains(key)) return;
        try
        {
            action();
            succeeded.Add(key);
        }
        catch (Exception ex)
        {
            failed.Add((key, ex.Message));
        }
    }


    private StoreBlob DefaultStoreBlobSettings(StoreBlob blob)
    {
        var newBlob = JsonConvert.DeserializeObject<StoreBlob>(JsonConvert.SerializeObject(blob));
        newBlob.EmailSettings = null;
        newBlob.PrimaryRateSettings = null;
        newBlob.FallbackRateSettings = null;
        newBlob.ShowPayInWalletButton = false;
        newBlob.ShowStoreHeader = false;
        newBlob.CelebratePayment = false;
        newBlob.PlaySoundOnPayment = false;
        newBlob.OnChainWithLnInvoiceFallback = false;
        newBlob.LightningAmountInSatoshi = false;
        newBlob.LazyPaymentMethods = false;
        newBlob.RedirectAutomatically = false;
        newBlob.ReceiptOptions = null;
        newBlob.HtmlTitle = string.Empty;
        newBlob.StoreSupportUrl = string.Empty;
        newBlob.AutoDetectLanguage = false;
        newBlob.DefaultLang = string.Empty;
        newBlob.LogoUrl = null;
        newBlob.CssUrl = null;
        newBlob.BrandColor = string.Empty;
        newBlob.ApplyBrandColorToBackend = false;
        return newBlob;
    }

    public byte[] CreateExport(StoreExportData exportData, string storeId, bool compress = true)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(exportData, JsonOptions));
        byte[] dataToEncrypt = compress ? CompressData(jsonBytes) : jsonBytes;
        return EncryptData(dataToEncrypt, storeId, compress);
    }

    public StoreExportData ParseExport(byte[] encryptedData)
    {
        var (data, wasCompressed) = DecryptData(encryptedData);
        var jsonBytes = wasCompressed ? DecompressData(data) : data;
        return System.Text.Json.JsonSerializer.Deserialize<StoreExportData>(Encoding.UTF8.GetString(jsonBytes), JsonOptions);
    }

    public StoreExportData GetExportPreview(byte[] encryptedData)
    {
        try
        {
            return ParseExport(encryptedData);
        }
        catch (Exception){ return null; }
    }

    public List<string> GetAvailableImportOptions(byte[] encryptedData)
    {
        try
        {
            var exportData = ParseExport(encryptedData);
            if (!string.IsNullOrEmpty(exportData.SelectedOptions))
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(exportData.SelectedOptions,
                    JsonOptions) ?? new List<string>();
            }
            return new();
        }
        catch (Exception){ return new(); }
    }

    private byte[] CompressData(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private byte[] DecompressData(byte[] compressedData)
    {
        using var input = new MemoryStream(compressedData);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(input, CompressionMode.Decompress))
        {
            gzip.CopyTo(output);
        }
        return output.ToArray();
    }

    private byte[] EncryptData(byte[] data, string storeId, bool compressed)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        var key = DeriveKey(storeId);
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor(key, aes.IV);
        var encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);

        // Build file: HEADER + FLAGS + IV_LENGTH + IV + DATA_LENGTH + DATA
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        writer.Write(Encoding.ASCII.GetBytes(MAGIC_HEADER));
        writer.Write((byte)(compressed ? 1 : 0));
        var storeIdBytes = Encoding.UTF8.GetBytes(storeId);
        writer.Write(storeIdBytes.Length);
        writer.Write(storeIdBytes);
        writer.Write(aes.IV.Length);
        writer.Write(aes.IV);
        writer.Write(encrypted.Length);
        writer.Write(encrypted);
        return output.ToArray();
    }

    private (byte[] data, bool compressed) DecryptData(byte[] encryptedData)
    {
        using var input = new MemoryStream(encryptedData);
        using var reader = new BinaryReader(input);

        var headerBytes = reader.ReadBytes(MAGIC_HEADER.Length);
        var header = Encoding.ASCII.GetString(headerBytes);

        if (header != MAGIC_HEADER)
            throw new InvalidDataException("Invalid export file format. This file may be corrupted or not a valid BTCPay storebridge plugin export");

        var compressed = reader.ReadByte() == 1;

        var storeIdLength = reader.ReadInt32();
        var storeIdBytes = reader.ReadBytes(storeIdLength);
        var originalStoreId = Encoding.UTF8.GetString(storeIdBytes);

        var ivLength = reader.ReadInt32();
        var iv = reader.ReadBytes(ivLength);

        var dataLength = reader.ReadInt32();
        var encrypted = reader.ReadBytes(dataLength);

        var key = DeriveKey(originalStoreId);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);

        return (decrypted, compressed);
    }

    private byte[] DeriveKey(string storeId)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(storeId, Encoding.UTF8.GetBytes("BTCPayServerStoreBridge_v1"), 600000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32);
    }
}
