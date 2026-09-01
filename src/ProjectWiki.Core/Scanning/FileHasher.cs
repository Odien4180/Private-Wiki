using System.Security.Cryptography;

namespace ProjectWiki.Core.Scanning;

public static class FileHasher
{
    /// <summary>Computes a lower-case hex SHA-256 hash of the given file's contents.</summary>
    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexStringLower(hash);
    }
}
