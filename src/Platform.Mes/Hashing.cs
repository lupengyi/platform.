using System.Security.Cryptography;
using System.Text;

namespace Platform.Mes;

public static class Hashing
{
    public static string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return ComputeHash(stream);
    }

    public static string ComputeContentHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        using var stream = new MemoryStream(bytes, writable: false);
        return ComputeHash(stream);
    }

    private static string ComputeHash(Stream stream)
    {
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(stream);
        var builder = new StringBuilder(hashBytes.Length * 2);
        foreach (var value in hashBytes)
        {
            builder.Append(value.ToString("x2"));
        }

        return builder.ToString();
    }
}
