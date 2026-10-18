# SPT Server

Community fork of the [SP-Tushonka C# server](https://github.com/SP-Tushonka/server-csharp), used with the [custom launcher](https://github.com/IGSApex/launcher) and Fika components. The current development target is SPT 4.1.6.

## Community additions

### Launcher account system

The launcher can probe `/launcher/v2/launcher-account-required` to determine whether server-specific account authentication is enabled. Account endpoints under `/launcher/v2/account/` support registration, login, session checks and account-associated profiles. The server remains authoritative for authentication and profile ownership; existing game profiles on disk should not be deleted during migration.

`LauncherAccountStore` manages the account/session data. Session tokens are random bearer credentials; the current implementation persists **hashes** of tokens in `sessions.json` and applies a 30-day expiration. Clients should protect their saved tokens and should not treat a remembered login as permanent authorization.

### Client mod manifest and downloads

`ClientModDistributionService` discovers the server's `BepInEx` directory relative to the working directory or executable directory, including the parent of `SPT_Runtime`. It recursively enumerates:

```text
SPT_Server/
├── BepInEx/
│   ├── plugins/
│   │   ├── Fika/Fika.Core.dll
│   │   └── WTT-ArmoryClient/...
│   └── patchers/
│       └── WTT-ContentBackportPatcher/...
└── SPT_Runtime/
```

The service publishes relative paths, names, file versions, lengths and SHA-256 hashes through `GET /launcher/v2/client-mods`. The launcher requests file contents using `/files/client-mod/{relative-path}`. Paths are checked to prevent traversal outside the BepInEx root. Standard `plugins/spt/`, `patchers/spt-prepatch.dll`, and `patchers/FixPluginTypesSerialization.dll` are excluded.

**Important:** This scans actual files, not a curated mod registry. Non-DLL files in eligible folders may appear as entries. Keep only files you intend to distribute in these directories; review licensing and secrets before making a server accessible. This is not a package manager or cryptographic signature system. A client trusting this endpoint may execute the distributed DLLs.

### Other functionality

Core SPT profile, mod, bundle, webserver and gameplay endpoints remain in place. Fika multiplayer functionality is maintained separately in [IGSApex/Fika-Server-CSharp](https://github.com/IGSApex/Fika-Server-CSharp).

## Diagnosing Mod Manager

1. Open `http://SERVER:PORT/launcher/v2/client-mods` from an authorized test machine.
2. Confirm the JSON array contains expected `plugins/...` and `patchers/...` paths, sizes and hashes.
3. If empty, verify the server process's working directory and that `BepInEx` is beside `SPT_Runtime` (or otherwise discoverable).
4. Test one download and verify its hash before rolling out to players.

The manifest and file-transfer features have not been fully end-to-end validated. Test on a nonproduction installation.

## Related projects

[Launcher](https://github.com/IGSApex/launcher) · [Fika client](https://github.com/IGSApex/Fika-Plugin) · [Fika server](https://github.com/IGSApex/Fika-Server-CSharp)

Original SPT/Tushonka authors retain credit for upstream work. Consult the repository's existing license files for terms.
