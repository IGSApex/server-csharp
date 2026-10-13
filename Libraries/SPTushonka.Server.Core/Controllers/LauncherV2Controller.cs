using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Launcher;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Launcher;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Utils;
using Info = SPTarkov.Server.Core.Models.Eft.Profile.Info;

namespace SPTarkov.Server.Core.Controllers;

[Injectable]
public class LauncherV2Controller(
    IReadOnlyList<SptMod> loadedMods,
    IReadOnlyList<ModPage> modPages,
    TemplateTable templateTable,
    HashUtil hashUtil,
    SaveServer saveServer,
    ServerLocalisationService serverLocalisationService,
    CoreConfig coreConfig,
    Watermark watermark,
    ProfileController profileController,
    LauncherAccountStore launcherAccounts,
    ILogger<LauncherV2Controller> logger
)
{
    /// <summary>
    ///     Returns a simple string of Pong!
    /// </summary>
    /// <returns></returns>
    public string Ping()
    {
        return "Pong!";
    }

    /// <summary>
    ///     Returns all available profile types and descriptions for creation.
    ///     - This is also localised.
    /// </summary>
    /// <returns>dict of profile names + description</returns>
    public Dictionary<string, string> Types()
    {
        var result = new Dictionary<string, string>();
        var dbProfiles = templateTable.Profiles;

        foreach (var (templateName, template) in dbProfiles)
        {
            if (IsEditionBlacklisted(templateName))
            {
                continue;
            }

            result.TryAdd(templateName, serverLocalisationService.GetText(template.DescriptionLocaleKey));
        }

        return result;
    }

    /// <summary>
    ///     Checks if login details were correct.
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public bool Login(LoginRequestData info)
    {
        var sessionId = GetSessionId(info);

        if (sessionId.IsEmpty) return false;
        if (!coreConfig.Features.RequireLauncherAccount) return true;
        if (!launcherAccounts.Owns(info.LauncherToken, sessionId.ToString())) return false;
        return true;
    }

    /// <summary>
    ///     Register a new profile.
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public async Task<bool> Register(RegisterData info)
    {
        if (coreConfig.Features.RequireLauncherAccount)
        {
            var account = launcherAccounts.Authenticate(info.LauncherToken);
            if (account is null)
            {
                logger.LogWarning("Profile registration rejected: launcher token is not authenticated");
                return false;
            }
            if (!string.Equals(info.Edition, account.EditionAllowed, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Profile registration rejected: requested edition {RequestedEdition} differs from allowed edition {AllowedEdition}", info.Edition, account.EditionAllowed);
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(info.Edition))
        {
            logger.LogWarning("Profile registration rejected: edition is empty");
            return false;
        }
        if (IsEditionBlacklisted(info.Edition))
        {
            logger.LogWarning("Profile registration rejected: edition {Edition} is blacklisted", info.Edition);
            return false;
        }
        if (!Types().ContainsKey(info.Edition))
        {
            logger.LogWarning("Profile registration rejected: edition {Edition} is missing from available profile templates", info.Edition);
            return false;
        }
        if (string.IsNullOrWhiteSpace(info.Username))
        {
            logger.LogWarning("Profile registration rejected: profile username is empty");
            return false;
        }

        foreach (var (_, profile) in saveServer.GetProfiles())
        {
            if (info.Username == profile.ProfileInfo?.Username)
            {
                logger.LogWarning("Profile registration rejected: username {Username} already exists", info.Username);
                return false;
            }
        }

        var profileId = await CreateAccount(info);
        if (coreConfig.Features.RequireLauncherAccount && !launcherAccounts.AddProfile(info.LauncherToken, profileId.ToString()))
        {
            logger.LogError("Profile registration failed: could not associate profile {ProfileId} with launcher account", profileId);
            saveServer.RemoveProfile(profileId);
            return false;
        }
        logger.LogInformation("Profile registration succeeded for profile {ProfileId}", profileId);
        return true;
    }

    /// <summary>
    ///     Remove profile from server.
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public bool Remove(LoginRequestData info)
    {
        var sessionId = GetSessionId(info);

        if (!Login(info)) return false;
        if (coreConfig.Features.RequireLauncherAccount && !launcherAccounts.Owns(info.LauncherToken, sessionId.ToString())) return false;
        if (!saveServer.RemoveProfile(sessionId)) return false;
        return !coreConfig.Features.RequireLauncherAccount || launcherAccounts.RemoveProfile(info.LauncherToken, sessionId.ToString());
    }

    /// <summary>
    ///     Gets the Servers SPT Version.
    ///     - "4.0.0"
    /// </summary>
    /// <returns></returns>
    public string SptVersion()
    {
        return watermark.GetVersionTag();
    }

    /// <summary>
    ///     Gets the compatible EFT Version.
    ///     - "0.14.9.31124"
    /// </summary>
    /// <returns></returns>
    public string EftVersion()
    {
        return coreConfig.CompatibleTarkovVersion;
    }

    /// <summary>
    ///     Gets the Servers loaded mods.
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, IModMetadata> LoadedMods()
    {
        return loadedMods.ToDictionary(sptMod => sptMod.ModMetadata.Name, sptMod => sptMod.ModMetadata);
    }

    /// <summary>
    ///     Gets a list of mod-registered SIC pages.
    /// </summary>
    public List<ModPage> ModPages()
    {
        return modPages.ToList();
    }

    /// <summary>
    ///     Creates the account from provided details.
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    protected async Task<MongoId> CreateAccount(RegisterData info)
    {
        var profileId = new MongoId();
        var scavId = new MongoId();
        var newProfileDetails = new Info
        {
            ProfileId = profileId,
            ScavengerId = scavId,
            Aid = hashUtil.GenerateAccountId(),
            Username = info.Username,
            Password = null,
            IsWiped = true,
            Edition = info.Edition,
        };

        saveServer.CreateProfile(newProfileDetails);

        await saveServer.LoadProfileAsync(profileId);
        await saveServer.SaveProfileAsync(profileId);

        return profileId;
    }

    // Per-game-profile passwords are superseded by launcher account sessions.
    public bool RequireLauncherAccount() => coreConfig.Features.RequireLauncherAccount;

    protected bool IsEditionBlacklisted(string? edition)
    {
        return edition is not null
            && coreConfig.Features.CreateNewProfileTypesBlacklist.Contains(edition, StringComparer.OrdinalIgnoreCase);
    }

    protected MongoId GetSessionId(LoginRequestData info)
    {
        foreach (var (sessionId, profile) in saveServer.GetProfiles())
        {
            if (info.Username == profile.ProfileInfo!.Username)
            {
                return sessionId;
            }
        }

        return MongoId.Empty();
    }

    public MiniProfile? GetMiniProfileFromUsername(LoginRequestData info)
    {
        return Login(info) ? profileController.GetMiniProfile(GetSessionId(info)) : null;
    }

    public bool Wipe(RegisterData info)
    {
        if (!coreConfig.AllowProfileWipe)
        {
            return false;
        }

        var sessionIsValid = Login(info) && (!coreConfig.Features.RequireLauncherAccount || (launcherAccounts.Authenticate(info.LauncherToken) is { } account && (!string.IsNullOrWhiteSpace(account.EditionAllowed) && string.Equals(info.Edition, account.EditionAllowed, StringComparison.OrdinalIgnoreCase))));

        if (!sessionIsValid)
        {
            return sessionIsValid;
        }

        var profileInfo = saveServer.GetProfiles().FirstOrDefault(x => x.Value.ProfileInfo?.Username == info.Username).Value.ProfileInfo;

        profileInfo!.Edition = info.Edition;
        profileInfo.IsWiped = true;

        return sessionIsValid;
    }
}
