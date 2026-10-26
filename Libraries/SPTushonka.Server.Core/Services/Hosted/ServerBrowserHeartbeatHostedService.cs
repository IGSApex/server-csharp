using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SPTarkov.Server.Core.Services.Hosted;

/// <summary>Publishes opt-in SPT servers to the central directory without blocking game startup.</summary>
public sealed class ServerBrowserHeartbeatHostedService(
    ILogger<ServerBrowserHeartbeatHostedService> logger) : BackgroundService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var master = Environment.GetEnvironmentVariable("SPT_BROWSER_URL");
        var key = Environment.GetEnvironmentVariable("SPT_BROWSER_SERVER_TOKEN");
        if (string.IsNullOrWhiteSpace(master) || string.IsNullOrWhiteSpace(key))
        {
            logger.LogInformation("Server browser disabled: configure SPT_BROWSER_URL and a per-server SPT_BROWSER_SERVER_TOKEN.");
            return;
        }

        var endpoint = master.TrimEnd('/');
        var id = Environment.GetEnvironmentVariable("SPT_BROWSER_SERVER_ID");
        if (string.IsNullOrWhiteSpace(id))
        {
            var idFile = Path.Combine("user", "server-browser-id.txt");
            Directory.CreateDirectory("user");
            if (!File.Exists(idFile))
                File.WriteAllText(idFile, Guid.NewGuid().ToString("N"));
            id = File.ReadAllText(idFile).Trim();
        }

        var registered = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var core = JsonDocument.Parse(File.ReadAllText(Path.Combine("SPT_Data", "configs", "core.json")));
                var root = core.RootElement;
                var features = root.GetProperty("features");
                var visible = features.TryGetProperty("showOnServerBrowser", out var flag) && flag.GetBoolean();
                var name = root.GetProperty("serverName").GetString() ?? "SPT Server";
                var accounts = features.TryGetProperty("requireLauncherAccount", out var required) && required.GetBoolean();
                using var http = JsonDocument.Parse(File.ReadAllText(Path.Combine("SPT_Data", "configs", "http.json")));
                var httpRoot = http.RootElement;
                var host = httpRoot.GetProperty("ip").GetString();
                var port = httpRoot.GetProperty("port").GetInt32();
                var address = Uri.TryCreate($"http://{host}:{port}", UriKind.Absolute, out var parsed) ? parsed.ToString().TrimEnd('/') : null;
                if (!visible || string.IsNullOrWhiteSpace(address))
                {
                    if (registered)
                    {
                        using var remove = new HttpRequestMessage(HttpMethod.Delete, endpoint + "/api/servers/" + Uri.EscapeDataString(id));
                        remove.Headers.TryAddWithoutValidation("X-Server-Token", key);
                        using var response = await _http.SendAsync(remove, stoppingToken);
                        response.EnsureSuccessStatusCode();
                        registered = false;
                    }
                }
                else
                {
                    // Player counts must come from a future live session tracker; never fabricate them.
                    var maxPlayers = features.TryGetProperty("maxPlayers", out var configuredMax) && configuredMax.TryGetInt32(out var max) && max > 0 ? max : 10;
                    var payload = new
                    {
                        id, name, address, currentPlayers = 0, maxPlayers,
                        requireLauncherAccount = accounts, showOnServerBrowser = true
                    };
                    using var request = new HttpRequestMessage(HttpMethod.Post,
                        endpoint + (registered ? "/api/servers/heartbeat" : "/api/servers/register"))
                    {
                        Content = JsonContent.Create(payload)
                    };
                    request.Headers.TryAddWithoutValidation("X-Registration-Key", key);
                    using var response = await _http.SendAsync(request, stoppingToken);
                    response.EnsureSuccessStatusCode();
                    registered = true;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Server browser heartbeat failed; retrying in 30 seconds.");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
