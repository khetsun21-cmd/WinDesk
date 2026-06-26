using System.Text;

namespace MarketTicker.Services;

internal static class PasswordCrypto
{
    /// <summary>Decode an obfuscated password string (Base64).</summary>
    public static string Decode(string encoded)
    {
        if (string.IsNullOrEmpty(encoded)) return string.Empty;
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch
        {
            return string.Empty;
        }
    }
}
