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
    private string Root => Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "SPT_Runtime", "client-mods"));

    public List<ClientModManifestEntry> GetManifest()
    {
        if (!Directory.Exists(Root)) return [];
        return Directory.EnumerateFiles(Root, "*.dll", SearchOption.TopDirectoryOnly)
            .Select(path => new ClientModManifestEntry
            {
                Name = Path.GetFileNameWithoutExtension(path),
                FileName = Path.GetFileName(path),
                Version = System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileVersion ?? "",
                Size = new FileInfo(path).Length,
                Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()
            }).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public string? ResolveFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName) ||
            !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return null;
        var full = Path.GetFullPath(Path.Combine(Root, fileName));
        return File.Exists(full) && string.Equals(Path.GetDirectoryName(full), Root, StringComparison.OrdinalIgnoreCase) ? full : null;
    }
}
