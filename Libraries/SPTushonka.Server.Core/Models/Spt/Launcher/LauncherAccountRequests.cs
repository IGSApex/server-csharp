using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Utils;

namespace SPTarkov.Server.Core.Models.Spt.Launcher;

public record LauncherAccountRequest : IRequestData
{
    [JsonPropertyName("username")] public string? Username { get; set; }
    [JsonPropertyName("password")] public string? Password { get; set; }
    [JsonPropertyName("token")] public string? Token { get; set; }
}

public record LauncherAccountResponse
{
    public bool Response { get; set; }
    public string? Token { get; set; }
    public string? Username { get; set; }
    public string? EditionAllowed { get; set; }
    public List<string> SavedProfiles { get; set; } = [];
}
