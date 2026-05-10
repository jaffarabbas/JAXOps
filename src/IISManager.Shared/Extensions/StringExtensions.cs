using System.Security.Cryptography;
using System.Text;

namespace IISManager.Shared.Extensions;

public static class StringExtensions
{
    public static string ToSha256Hash(this string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string Truncate(this string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    public static bool IsNullOrEmpty(this string? value) => string.IsNullOrEmpty(value);
}
