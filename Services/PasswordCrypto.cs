using System.Text;

namespace WinDesk.Services;

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

    /// <summary>Decode obfuscated password back to plaintext.
    /// Handles three formats for backwards compatibility:
    /// 1. XOR+Base64 (current)
    /// 2. Plain Base64 (intermediate)
    /// 3. Plaintext (earliest)
    /// </summary>
    public static string Decode(string encoded)
    {
        if (string.IsNullOrEmpty(encoded)) return string.Empty;

        // Try XOR+Base64 (current format)
        try
        {
            var bytes = Convert.FromBase64String(encoded);
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] ^= XorKey[i % XorKey.Length];
            var result = Encoding.UTF8.GetString(bytes);
            if (IsReasonablePassword(result))
                return result;
        }
        catch { }

        // Try plain Base64 (intermediate format)
        try
        {
            var bytes = Convert.FromBase64String(encoded);
            var result = Encoding.UTF8.GetString(bytes);
            if (IsReasonablePassword(result))
                return result;
        }
        catch { }

        // Fallback: plaintext (earliest format)
        return encoded;
    }

    /// <summary>Check if a decoded string looks like a reasonable password
    /// (printable ASCII, no control characters or garbage bytes).</summary>
    private static bool IsReasonablePassword(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (var c in text)
        {
            if (c < 0x20 || c > 0x7E) return false;
        }
        return true;
    }
}
