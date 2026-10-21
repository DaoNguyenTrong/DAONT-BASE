using System.Security.Cryptography;
using System.Text;

namespace StarterKit.Infrastructure.Services.Storage;

internal sealed class StoragePathGenerator
{
    public string Generate(string fileName, string? folder = null)
    {
        string prefix = folder is null ? string.Empty : NormalizeFolder(folder) + "/";
        DateTime now = DateTime.UtcNow;
        string extension = Path.GetExtension(fileName);
        string timestamp = now.ToString("yyyyMMddHHmmss");
        string hash = CreateHash($"{fileName}_{Guid.NewGuid()}");

        return prefix + Path.Combine(
                now.ToString("yyyy"),
                now.ToString("MM"),
                now.ToString("dd"),
                $"{timestamp}_{hash}{extension}")
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string NormalizeFolder(string folder)
    {
        string[] segments = folder.Split('/');

        if (folder.Contains('\\') || segments.Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("Folder must be a relative path without empty or dot segments.", nameof(folder));
        }

        return folder;
    }

    private static string CreateHash(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }
}
