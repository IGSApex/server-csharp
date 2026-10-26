using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace SPTarkov.Server.Core.Services.Server;

[Injectable(InjectionType.OnLoad, OnLoadOrder.PostLoad)]
public sealed class ClientModManifestStartup(ClientModDistributionService clientMods) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        clientMods.RefreshManifestOnStartup();
        return Task.CompletedTask;
    }
}
