using System.Text;

namespace MarketTicker.Services;

internal static class PasswordCrypto
{
    private static readonly byte[] XorKey =
    [
        0x5A, 0x3C, 0x7E, 0x1F, 0x9B, 0x4D, 0xC2, 0x8A,
        0x6E, 0x2B, 0xF3, 0x0D, 0x81, 0x59, 0xAE, 0x47
    ];

    /// <summary>Encode plaintext password to obfuscated form.</summary>
    public static string Encode(string plain)
    {
        var bytes = Encoding.UTF8.GetBytes(plain);
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] ^= XorKey[i % XorKey.Length];
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Decode obfuscated password back to plaintext.</summary>
    public static string Decode(string encoded)
    {
        if (string.IsNullOrEmpty(encoded)) return string.Empty;
        try
        {
            var bytes = Convert.FromBase64String(encoded);
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] ^= XorKey[i % XorKey.Length];
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}
