using System.Security.Cryptography;
using System.Text;

namespace StarterKit.Application.Services.Auth;

internal static class TokenHash
{
    public static string Compute(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
