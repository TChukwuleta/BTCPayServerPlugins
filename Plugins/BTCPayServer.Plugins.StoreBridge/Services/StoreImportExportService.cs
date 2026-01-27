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
using BTCPayServer.Forms;
using BTCPayServer.Plugins.StoreBridge.ViewModels;
using BTCPayServer.Services.Apps;
using BTCPayServer.Services.Stores;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.StoreBridge.Services;

public class StoreImportExportService
{
    private readonly AppService _appService;
    private readonly StoreRepository _storeRepository;
    private readonly FormDataService _formDataService;
    private const string MAGIC_HEADER = "BTCPAY_STOREBRIDGE";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public StoreImportExportService(StoreRepository storeRepository, AppService appService, 
        FormDataService formDataService)
    {
        _appService = appService;
        _storeRepository = storeRepository;
        _formDataService = formDataService;
    }

    public async Task<StoreExportData> GetExportDataPreview(string sourceInstanceUrl, string userId, Data.StoreData store, List<string> selectedOptions)
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
            exportData.Store ??= new();
            exportData.Store.StoreBlob = JsonConvert.SerializeObject(blob);
        }
        if (selectedOptions.Contains("EmailSettings"))
        {
            blob.EmailSettings = originalBlob.EmailSettings;
        }
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
        if (selectedOptions.Contains("Forms"))
        {
            var forms = await _formDataService.GetForms(store.Id);
            exportData.Forms = forms.Select(c => new FormExport
            {
                Public = c.Public,
                Name = c.Name,
                Config = c.Config
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
                SettingsJson = JsonConvert.SerializeObject(app.App)
            }).ToList();
        }
        exportData.Store.StoreBlob = JsonConvert.SerializeObject(blob);
        return exportData;
    }

    public async Task<byte[]> ExportStore(string sourceInstanceUrl, string userId, Data.StoreData store, List<string> selectedOptions)
    {
        var exportData = await GetExportDataPreview(sourceInstanceUrl, userId, store, selectedOptions);
        var encryptedData = CreateExport(exportData, store.Id);
        return encryptedData;
    }

    public async Task<(bool Success, string Message)> ImportStore(Data.StoreData destinationStore, byte[] encryptedData,
    string userId, List<string> userSelectedOptions = null)
    {
        try
        {
            bool storeModified = false;
            var destinationStoreBlob = destinationStore.GetStoreBlob();
            var exportData = ParseExport(encryptedData);
            // In the future... factory method based on versions..

            var exportedOptions = !string.IsNullOrEmpty(exportData.SelectedOptions)
                ? JsonConvert.DeserializeObject<List<string>>(exportData.SelectedOptions) : new List<string>();

            var optionsToImport = userSelectedOptions != null
                ? userSelectedOptions.Intersect(exportedOptions).ToList() : exportedOptions;

            if (!optionsToImport.Any())
            {
                return (false, "No valid options selected for import");
            }

            if (exportData.Store != null && !string.IsNullOrEmpty(exportData.Store.StoreBlob))
            {
                var importedBlob = JsonConvert.DeserializeObject<StoreBlob>(exportData.Store.StoreBlob);
                if (optionsToImport.Contains("BrandingSettings"))
                {
                    destinationStoreBlob.LogoUrl = importedBlob.LogoUrl;
                    destinationStoreBlob.CssUrl = importedBlob.CssUrl;
                    destinationStoreBlob.BrandColor = importedBlob.BrandColor;
                    destinationStoreBlob.ApplyBrandColorToBackend = importedBlob.ApplyBrandColorToBackend;
                    storeModified = true;
                }
                if (optionsToImport.Contains("EmailSettings"))
                {
                    destinationStoreBlob.EmailSettings = importedBlob.EmailSettings;
                    storeModified = true;
                }
                if (optionsToImport.Contains("RateSettings"))
                {
                    destinationStoreBlob.PrimaryRateSettings = importedBlob.PrimaryRateSettings;
                    destinationStoreBlob.FallbackRateSettings = importedBlob.FallbackRateSettings;
                    if (Enum.TryParse<SpeedPolicy>(exportData.Store.SpeedPolicy, out var speedPolicy))
                    {
                        destinationStore.SpeedPolicy = speedPolicy;
                    }
                    storeModified = true;
                }
                if (optionsToImport.Contains("CheckoutSettings"))
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
                }
                if (storeModified)
                {
                    destinationStore.SetStoreBlob(destinationStoreBlob);
                }
            }

            if (optionsToImport.Contains("Webhooks") && exportData.Webhooks?.Any() == true)
            {
                foreach (var webhookExport in exportData.Webhooks.Where(c => !string.IsNullOrEmpty(c.BlobJson)))
                {
                    var webhookBlob = JsonConvert.DeserializeObject<WebhookBlob>(webhookExport.BlobJson);
                    await _storeRepository.CreateWebhook(destinationStore.Id, webhookBlob);
                }
            }
            if (optionsToImport.Contains("Roles") && exportData.Roles?.Any() == true)
            {
                foreach (var roleExport in exportData.Roles)
                {
                    StoreRoleId roleId = new StoreRoleId(destinationStore.Id, roleExport.Role);
                    await _storeRepository.AddOrUpdateStoreRole(roleId, roleExport.Permissions);
                }
            }
            if (optionsToImport.Contains("Forms") && exportData.Forms?.Any() == true)
            {
                foreach (var formExport in exportData.Forms)
                {
                    await _formDataService.AddOrUpdateForm(new FormData
                    {
                        StoreId = destinationStore.Id,
                        Name = formExport.Name,
                        Config = formExport.Config,
                        Public = formExport.Public
                    });
                }
            }

            if (optionsToImport.Contains("Apps") && exportData.Apps?.Any() == true)
            {
                foreach (var appExport in exportData.Apps)
                {
                    var appData = JsonConvert.DeserializeObject<AppData>(appExport.SettingsJson);
                    appData.StoreDataId = destinationStore.Id;
                    appData.Name = appExport.AppName;
                    appData.AppType = appExport.AppType;
                    appData.Id = null;
                    appData.Created = DateTime.UtcNow;
                    await _appService.UpdateOrCreateApp(appData);
                }
            }
            await _storeRepository.UpdateStore(destinationStore);

            var importedCount = optionsToImport.Count;
            var importedItems = string.Join(", ", optionsToImport.Select(o =>
                ImportViewModel.OptionMetadata.ContainsKey(o) ? ImportViewModel.OptionMetadata[o].Title : o));

            return (true, $"Successfully imported {importedCount} configuration(s): {importedItems}");
        }
        catch (Exception ex)
        {
            return (false, $"Import failed: {ex.Message}");
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

    public List<string> GetAvailableImportOptions(byte[] encryptedData, string storeId)
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
