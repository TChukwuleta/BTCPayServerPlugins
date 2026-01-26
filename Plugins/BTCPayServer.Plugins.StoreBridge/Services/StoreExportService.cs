using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BTCPayServer.Plugins.StoreBridge.ViewModels;

namespace BTCPayServer.Plugins.StoreBridge.Services;

public class StoreExportService
{
    private const string MAGIC_HEADER = "BTCPAY_STOREBRIDGE_V1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public byte[] CreateExport(StoreExportData exportData, string storeId, bool compress = true)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(exportData, JsonOptions));
        byte[] dataToEncrypt = compress ? CompressData(jsonBytes) : jsonBytes;
        return EncryptData(dataToEncrypt, storeId, compress);
    }

    public StoreExportData ParseExport(byte[] encryptedData, string storeId)
    {
        var (data, wasCompressed) = DecryptData(encryptedData, storeId);
        var jsonBytes = wasCompressed ? DecompressData(data) : data;
        return JsonSerializer.Deserialize<StoreExportData>(Encoding.UTF8.GetString(jsonBytes), JsonOptions);
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
        writer.Write(aes.IV.Length);
        writer.Write(aes.IV);
        writer.Write(encrypted.Length);
        writer.Write(encrypted);
        return output.ToArray();
    }

    private (byte[] data, bool compressed) DecryptData(byte[] encryptedData, string storeId)
    {
        using var input = new MemoryStream(encryptedData);
        using var reader = new BinaryReader(input);

        // Verify header
        var headerBytes = reader.ReadBytes(MAGIC_HEADER.Length);
        var header = Encoding.ASCII.GetString(headerBytes);

        if (header != MAGIC_HEADER)
            throw new InvalidDataException("Invalid export file format. This file may be corrupted or not a valid BTCPay export.");

        // Read flags
        var compressed = reader.ReadByte() == 1;

        // Read IV
        var ivLength = reader.ReadInt32();
        var iv = reader.ReadBytes(ivLength);

        // Read encrypted data
        var dataLength = reader.ReadInt32();
        var encrypted = reader.ReadBytes(dataLength);

        // Decrypt
        var key = DeriveKey(storeId);

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

    private byte[] DeriveKey(string storeId)
    {
        // Use PBKDF2 to derive a consistent encryption key from store ID
        // This allows the same store to decrypt its own exports
        using var pbkdf2 = new Rfc2898DeriveBytes(
            storeId,
            Encoding.UTF8.GetBytes("BTCPayServerStoreBridge_v1"), // Salt
            100000, // Iterations
            HashAlgorithmName.SHA256
        );

        return pbkdf2.GetBytes(32); // 256-bit key
    }
}
