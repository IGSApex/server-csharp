using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Routers.Dynamic;

[Injectable(TypePriority = OnLoadOrder.Routers)]
public class ClientModDynamicRouter(JsonUtil jsonUtil)
    : DynamicRouter(jsonUtil, [
        new RouteAction<EmptyRequestData>("/files/client-mod", (_, _, _, _, _) => new ValueTask<string>("CLIENT_MOD"))
    ]) { }
