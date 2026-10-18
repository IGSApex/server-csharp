using Microsoft.AspNetCore.Http;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Services.Server;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Routers.Serializers;

[Injectable]
public class ClientModSerializer(ClientModDistributionService mods, HttpFileUtil files) : ISerializer
{
    public bool CanHandle(string route) => route == "CLIENT_MOD";

    public async Task SerializeAsync(MongoId sessionID, HttpRequest req, HttpResponse resp,
        object? body, CancellationToken cancellationToken = default)
    {
        var filename = req.Path.Value?.Split("/files/client-mod/", StringSplitOptions.None).LastOrDefault();
        var path = mods.ResolveFile(Uri.UnescapeDataString(filename ?? ""));
        if (path is null)
        {
            resp.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await files.SendFileAsync(resp, path, cancellationToken);
    }
}
