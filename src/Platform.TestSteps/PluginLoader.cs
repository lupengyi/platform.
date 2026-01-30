using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using Platform.Contracts;

namespace Platform.TestSteps;

public sealed class PluginLoader
{
    public IReadOnlyCollection<Assembly> LoadAssemblies(PluginLoadOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var pluginDirectory = Path.GetFullPath(options.PluginDirectory);
        if (!IsLocalPath(pluginDirectory) || !Directory.Exists(pluginDirectory))
        {
            return Array.Empty<Assembly>();
        }

        var assemblies = new List<Assembly>();
        foreach (var file in Directory.EnumerateFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            var fullPath = Path.GetFullPath(file);
            if (!fullPath.StartsWith(pluginDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsTrustedPlugin(fullPath, options.Security))
            {
                continue;
            }

            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
            assemblies.Add(assembly);
        }

        return assemblies;
    }

    private static bool IsLocalPath(string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsUnc)
        {
            return false;
        }

        return Path.IsPathRooted(path);
    }

    private static bool IsTrustedPlugin(string path, PluginSecurityOptions security)
    {
        if (HasStrongName(path))
        {
            return true;
        }

        var hash = ComputeSha256(path);
        return security.AllowedSha256.Any(allowed =>
            string.Equals(allowed, hash, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasStrongName(string path)
    {
        try
        {
            var name = AssemblyName.GetAssemblyName(path);
            var token = name.GetPublicKeyToken();
            return token is { Length: > 0 };
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}
