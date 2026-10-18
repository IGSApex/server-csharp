# SPT Server

SPT game server with launcher account support and client mod distribution.

## Features

- **Launcher accounts:** Servers can require users to register or log in before accessing their game profiles.
- **Account profiles:** Game profiles are associated with launcher accounts and managed through the launcher.
- **Remembered sessions:** The server validates saved launcher sessions for returning users.
- **Client mod manifest:** The server lists client mods from its `BepInEx/plugins` and `BepInEx/patchers` directories, including subfolders.
- **Client mod downloads:** The launcher can retrieve the server's client mod files and match them against the player's installation.

## How to Use

Place the client mods you want players to use in the server's `BepInEx/plugins` or `BepInEx/patchers` directories. Players can open **Mod Manager** in the launcher, select **Refresh**, and use **Update Mods to Match Server** to synchronize their client mods.

Standard SPT modules are excluded from the optional mod list.

## Related Projects

[Launcher](https://github.com/IGSApex/launcher) · [Modules](https://github.com/IGSApex/modules) · [Fika Client](https://github.com/IGSApex/Fika-Plugin) · [Fika Server](https://github.com/IGSApex/Fika-Server-CSharp)
