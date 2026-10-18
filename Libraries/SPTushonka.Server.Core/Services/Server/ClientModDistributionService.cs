using System.Security.Cryptography;
using SPTarkov.DI.Annotations;

namespace SPTarkov.Server.Core.Services.Server;

public sealed class ClientModManifestEntry
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string FileName { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
}

[Injectable(InjectionType.Singleton)]
public class ClientModDistributionService
{
    // The server executable commonly runs from SPT_Runtime while BepInEx is one level above.
    private string Root
    {
        get
        {
            var candidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "BepInEx"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "BepInEx"),
                Path.Combine(AppContext.BaseDirectory, "BepInEx"),
                Path.Combine(AppContext.BaseDirectory, "..", "BepInEx")
            };
            return candidates.Select(Path.GetFullPath)
                .FirstOrDefault(path => Directory.Exists(Path.Combine(path, "plugins")) ||
                                        Directory.Exists(Path.Combine(path, "patchers")))
                ?? Path.GetFullPath(candidates[0]);
        }
    }

    private static bool IsBuiltIn(string relative)
    {
        var path = relative.Replace('\\', '/');
        return path.StartsWith("plugins/spt/", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("patchers/spt-prepatch.dll", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("patchers/FixPluginTypesSerialization.dll", StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<string> EnumerateClientFiles()
    {
        foreach (var folder in new[] { "plugins", "patchers" })
        {
            var directory = Path.Combine(Root, folder);
            if (!Directory.Exists(directory)) continue;
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(Root, file).Replace('\\', '/');
                if (!IsBuiltIn(relative) && !relative.Split('/').Any(part => part.StartsWith('.')))
                    yield return file;
            }
        }
    }

    public List<ClientModManifestEntry> GetManifest()
    {
        if (!Directory.Exists(Root)) return [];
        return EnumerateClientFiles().Select(path =>
        {
            var relative = Path.GetRelativePath(Root, path).Replace('\\', '/');
            using var stream = File.OpenRead(path);
            return new ClientModManifestEntry
            {
                Name = Path.GetFileNameWithoutExtension(path),
                FileName = relative,
                Version = System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileVersion ?? "",
                Size = stream.Length,
                Sha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant()
            };
        }).OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public string? ResolveFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        var relative = fileName.Replace('\\', '/');
        var parts = relative.Split('/');
        if (parts.Length < 2 || (parts[0] != "plugins" && parts[0] != "patchers") ||
            parts.Any(part => part is "" or "." or "..") || IsBuiltIn(relative)) return null;
        var full = Path.GetFullPath(Path.Combine(Root, Path.Combine(parts)));
        var rootPrefix = Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) && File.Exists(full) ? full : null;
    }
}
