using System.Security.Cryptography;
using System.Text;

namespace MOMBotPro.API.Services;

/// <summary>
/// AES-256-CBC encryption singleton.
/// Key: Encryption:Key config value — must be a 32-byte base64 string.
/// Falls back to SHA-256 derivation when the value is not a valid 32-byte base64.
/// Ciphertext format: Base64( 16-byte IV | ciphertext )
/// </summary>
public class AesEncryptor
{
    private readonly byte[] _key;

    public AesEncryptor(IConfiguration config)
    {
        var raw = config["Encryption:Key"] ?? "";
        _key = TryLoadBase64Key(raw)
            ?? SHA256.HashData(Encoding.UTF8.GetBytes(
                string.IsNullOrEmpty(raw) ? "MOMBotProDefaultEncKey!@#$%^&*()" : raw));
    }

    // ── Public API ────────────────────────────────────────────────────────

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var cipherBytes = encryptor.TransformFinalBlock(
            Encoding.UTF8.GetBytes(plaintext), 0,
            Encoding.UTF8.GetByteCount(plaintext));

        // Format: Base64( 16-byte IV | ciphertext )
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        aes.IV.CopyTo(result, 0);
        cipherBytes.CopyTo(result, aes.IV.Length);
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return ciphertext;
        try
        {
            var data = Convert.FromBase64String(ciphertext);
            if (data.Length <= 16) return ciphertext;   // too short — treat as plaintext

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV  = data[..16];

            using var decryptor = aes.CreateDecryptor();
            var plain = decryptor.TransformFinalBlock(data, 16, data.Length - 16);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return ciphertext;   // not Base64 or wrong key → legacy plaintext token
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static byte[]? TryLoadBase64Key(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        try
        {
            var bytes = Convert.FromBase64String(value);
            return bytes.Length == 32 ? bytes : null;
        }
        catch
        {
            return null;
        }
    }
}
