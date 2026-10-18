using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.DI.Annotations;

namespace SPTarkov.Server.Core.Controllers;

/// <summary>
/// Separate launcher identities. Existing EFT profile files are deliberately not imported.
/// The JSON account record is persisted under user/profiles/launcher/{username}.json.
/// Remembered session tokens are stored as SHA-256 hashes on disk and expire after 30 days.
/// </summary>
[Injectable]
public class LauncherAccountStore
{
    private readonly object _sync = new();
    // Shared across DI-created store instances so account login and profile registration
    // validate against the same live server session registry.
    private static readonly ConcurrentDictionary<string, LauncherSession> _sessions = new();
    private readonly string _directory;
    private readonly string _sessionsFile;
    private static readonly object SessionFileLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public LauncherAccountStore()
    {
        _directory = Path.Combine(AppContext.BaseDirectory, "user", "profiles", "launcher");
        Directory.CreateDirectory(_directory);
        _sessionsFile = Path.Combine(_directory, "sessions.json");
    }

    private static bool ValidUsername(string? username) =>
        username is { Length: >= 3 and <= 48 }
        && username.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    private string FileFor(string username) => Path.Combine(_directory, username.ToLowerInvariant() + ".json");

    private LauncherAccount? Read(string username)
    {
        var path = FileFor(username);
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<LauncherAccount>(File.ReadAllText(path), JsonOptions);
    }

    private void Write(LauncherAccount account)
    {
        var path = FileFor(account.Username);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(account, JsonOptions));
            File.Move(temp, path, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public bool Register(string? username, string? password, string editionAllowed = "Standard")
    {
        if (!ValidUsername(username) || !Passwords.IsValid(password)) return false;
        lock (_sync)
        {
            if (Read(username!) is not null) return false;
            Write(new LauncherAccount
            {
                Username = username!,
                Password = Passwords.Hash(password!),
                EditionAllowed = editionAllowed,
                SavedProfiles = []
            });
            return true;
        }
    }

    public LauncherAuthentication? Login(string? username, string? password)
    {
        if (!ValidUsername(username)) return null;
        lock (_sync)
        {
            var account = Read(username!);
            if (account is null || !Passwords.Verify(account.Password, password)) return null;
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var session = new LauncherSession(account.Username, DateTimeOffset.UtcNow.AddDays(30));
            _sessions[token] = session;
            SaveSession(token, session);
            return new LauncherAuthentication(token, account.Username, account.EditionAllowed, [.. account.SavedProfiles]);
        }
    }

    public LauncherAuthentication? Authenticate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        if (!_sessions.TryGetValue(token, out var session))
        {
            session = LoadSession(token);
            if (session is null) return null;
            _sessions[token] = session;
        }
        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(token, out _);
            RemoveSession(token);
            return null;
        }
        lock (_sync)
        {
            var account = Read(session.Username);
            return account is null ? null : new LauncherAuthentication(token, account.Username, account.EditionAllowed, [.. account.SavedProfiles]);
        }
    }

    public bool Owns(string? token, string profileId) =>
        Authenticate(token)?.SavedProfiles.Contains(profileId, StringComparer.OrdinalIgnoreCase) == true;

    public bool AddProfile(string? token, string profileId)
    {
        var auth = Authenticate(token);
        if (auth is null || string.IsNullOrWhiteSpace(profileId)) return false;
        lock (_sync)
        {
            var account = Read(auth.Username);
            if (account is null) return false;
            if (!account.SavedProfiles.Contains(profileId, StringComparer.OrdinalIgnoreCase))
                account.SavedProfiles.Add(profileId);
            Write(account);
            return true;
        }
    }

    public bool RemoveProfile(string? token, string profileId)
    {
        var auth = Authenticate(token);
        if (auth is null) return false;
        lock (_sync)
        {
            var account = Read(auth.Username);
            if (account is null || account.SavedProfiles.RemoveAll(x => string.Equals(x, profileId, StringComparison.OrdinalIgnoreCase)) == 0)
                return false;
            Write(account);
            return true;
        }
    }

    public void Logout(string? token)
    {
        if (token is not null)
        {
            _sessions.TryRemove(token, out _);
            RemoveSession(token);
        }
    }

    private static string TokenKey(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private Dictionary<string, LauncherSession> ReadSessions()
    {
        if (!File.Exists(_sessionsFile)) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, LauncherSession>>(
                File.ReadAllText(_sessionsFile), JsonOptions) ?? [];
        }
        catch (JsonException) { return []; }
    }

    private void UpdateSessions(Action<Dictionary<string, LauncherSession>> update)
    {
        lock (SessionFileLock)
        {
            var sessions = ReadSessions();
            update(sessions);
            var temp = _sessionsFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(sessions, JsonOptions));
                File.Move(temp, _sessionsFile, true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    private void SaveSession(string token, LauncherSession session) =>
        UpdateSessions(sessions => sessions[TokenKey(token)] = session);

    private LauncherSession? LoadSession(string token)
    {
        lock (SessionFileLock)
        {
            return ReadSessions().TryGetValue(TokenKey(token), out var session) ? session : null;
        }
    }

    private void RemoveSession(string token) =>
        UpdateSessions(sessions => sessions.Remove(TokenKey(token)));

    private sealed record LauncherSession(string Username, DateTimeOffset ExpiresAt);
}

public sealed class LauncherAccount
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("editionAllowed")]
    public string EditionAllowed { get; set; } = "Standard";

    [JsonPropertyName("savedProfiles")]
    public List<string> SavedProfiles { get; set; } = [];
}

public sealed record LauncherAuthentication(string Token, string Username, string EditionAllowed, List<string> SavedProfiles);
