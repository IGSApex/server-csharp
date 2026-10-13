using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Eft.Launcher;
using SPTarkov.Server.Core.Models.Spt.Launcher;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Callbacks;

[Injectable]
public class LauncherV2Callbacks(
    HttpResponseUtil httpResponseUtil,
    LauncherV2Controller launcherV2Controller,
    ProfileController profileController,
    LauncherAccountStore launcherAccounts
)
{
    public ValueTask<string> AccountRegister(LauncherAccountRequest request)
    {
        var success = launcherAccounts.Register(request.Username, request.Password);
        return new ValueTask<string>(httpResponseUtil.NoBody(new LauncherAccountResponse { Response = success }));
    }

    public ValueTask<string> AccountLogin(LauncherAccountRequest request)
    {
        var session = launcherAccounts.Login(request.Username, request.Password);
        return new ValueTask<string>(httpResponseUtil.NoBody(new LauncherAccountResponse
        {
            Response = session is not null,
            Token = session?.Token,
            Username = session?.Username,
            EditionAllowed = session?.EditionAllowed,
            SavedProfiles = session?.SavedProfiles ?? []
        }));
    }

    public ValueTask<string> AccountSession(LauncherAccountRequest request)
    {
        var session = launcherAccounts.Authenticate(request.Token);
        return new ValueTask<string>(httpResponseUtil.NoBody(new LauncherAccountResponse
        {
            Response = session is not null,
            Token = session?.Token,
            Username = session?.Username,
            EditionAllowed = session?.EditionAllowed,
            SavedProfiles = session?.SavedProfiles ?? []
        }));
    }

    public ValueTask<string> AccountLogout(LauncherAccountRequest request)
    {
        launcherAccounts.Logout(request.Token);
        return new ValueTask<string>(httpResponseUtil.NoBody(new LauncherAccountResponse { Response = true }));
    }

    public ValueTask<string> Ping()
    {
        return new ValueTask<string>(httpResponseUtil.NoBody(new LauncherV2PingResponse { Response = launcherV2Controller.Ping() }));
    }

    public ValueTask<string> PasswordRequired()
    {
        return new ValueTask<string>(httpResponseUtil.NoBody(
            new LauncherV2AuthSettingsResponse { Response = launcherV2Controller.RequireLauncherAccount() }));
    }

    public ValueTask<string> Types()
    {
        return new ValueTask<string>(httpResponseUtil.NoBody(new LauncherV2TypesResponse { Response = launcherV2Controller.Types() }));
    }

    public ValueTask<string> Login(LoginRequestData info)
    {
        return new ValueTask<string>(httpResponseUtil.NoBody(new LauncherV2LoginResponse { Response = launcherV2Controller.Login(info) }));
    }

    public async ValueTask<string> Register(RegisterData info)
    {
        return httpResponseUtil.NoBody(
            new LauncherV2RegisterResponse
            {
                Response = await launcherV2Controller.Register(info),
                Profiles = profileController.GetMiniProfiles().Where(p => !launcherV2Controller.RequireLauncherAccount() || (p.ProfileId is not null && launcherAccounts.Owns(info.LauncherToken, p.ProfileId))).ToList(),
            }
        );
    }

    public ValueTask<string> Remove(LoginRequestData info)
    {
        return new ValueTask<string>(
            httpResponseUtil.NoBody(
                new LauncherV2RemoveResponse
                {
                    Response = launcherV2Controller.Remove(info),
                    Profiles = profileController.GetMiniProfiles().Where(p => p.ProfileId is not null && launcherAccounts.Owns(info.LauncherToken, p.ProfileId)).ToList(),
                }
            )
        );
    }

    public ValueTask<string> CompatibleVersion()
    {
        return new ValueTask<string>(
            httpResponseUtil.NoBody(new LauncherV2VersionResponse { Response = launcherV2Controller.SptVersion() })
        );
    }

    public ValueTask<string> Mods()
    {
        return new ValueTask<string>(httpResponseUtil.NoBody(new LauncherV2ModsResponse { Response = launcherV2Controller.LoadedMods() }));
    }

    public ValueTask<string> ModPages()
    {
        return new ValueTask<string>(
            httpResponseUtil.NoBody(new LauncherV2ModPagesResponse { Response = launcherV2Controller.ModPages() })
        );
    }

    public ValueTask<string> Profiles(LauncherAccountRequest request)
    {
        return new ValueTask<string>(
            httpResponseUtil.NoBody(new LauncherV2ProfilesResponse { Response = profileController.GetMiniProfiles().Where(p => !launcherV2Controller.RequireLauncherAccount() || (p.ProfileId is not null && launcherAccounts.Owns(request.Token, p.ProfileId))).ToList() })
        );
    }

    public ValueTask<string> Profile(LoginRequestData username)
    {
        return new ValueTask<string>(
            httpResponseUtil.NoBody(new LauncherV2ProfileResponse { Response = launcherV2Controller.GetMiniProfileFromUsername(username) })
        );
    }

    public ValueTask<string> Wipe(RegisterData info)
    {
        return new ValueTask<string>(
            httpResponseUtil.NoBody(
                new LauncherV2WipeResponse { Response = launcherV2Controller.Wipe(info), Profiles = profileController.GetMiniProfiles() }
            )
        );
    }
}
