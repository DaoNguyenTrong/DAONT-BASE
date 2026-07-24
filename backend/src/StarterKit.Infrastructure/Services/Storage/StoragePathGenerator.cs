using System.Security.Cryptography;
using System.Text;

namespace StarterKit.Infrastructure.Services.Storage;

internal sealed class StoragePathGenerator
{
    public string Generate(string fileName)
    {
        DateTime now = DateTime.UtcNow;
        string extension = Path.GetExtension(fileName);
        string timestamp = now.ToString("yyyyMMddHHmmss");
        string hash = CreateHash($"{fileName}_{Guid.NewGuid()}");

        return Path.Combine(
                now.ToString("yyyy"),
                now.ToString("MM"),
                now.ToString("dd"),
                $"{timestamp}_{hash}{extension}")
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string CreateHash(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }
}
