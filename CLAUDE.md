# CS2-SimpleAdmin

CounterStrikeSharp admin plugin. Fork at `github.com/mrc4tt/CS2-SimpleAdmin` (ex-Forgejo, gone). Upstream `daffyyyy/CS2-SimpleAdmin` dead — no upstream PRs.

## Layout
- `CS2-SimpleAdmin/` main plugin (`CS2-SimpleAdmin.cs`). `VERSION` = release version, bumped by the RE release (`build-all.sh`), not by hand.
- `CS2-SimpleAdminApi/` public API (`ICS2-SimpleAdminApi.cs`).
- `Modules/` — only `CS2-SimpleAdmin_FunCommands`, `CS2-SimpleAdmin_StealthModule` ship. Rest not in release.
- `.sln` = plugin + API only; each module own `.csproj`, built individually.
- Root namespace `CS2_SimpleAdmin` (underscore).

## Build
`dotnet build CS2-SimpleAdmin/CS2-SimpleAdmin.csproj -c Release` (net10.0, out `bin/Release/net10.0/`).
`<PublishTrimmed>` must stay off (CS2MenuManager reflection).

## DB
- Migrations `CS2-SimpleAdmin/Database/Migrations/{Mysql,Sqlite}/NNN_*.sql`, csproj glob copies them (no csproj edit). Both folders same count/intent; if SQLite diverges (e.g. `015_steamidToBigInt.sql`), header comment explains why.
- `IDatabaseProvider`: `SqliteDatabaseProvider` / `MySqlDatabaseProvider`. Dispatch dialect via `IsSqlite`, never path sniffing. `Migration.cs` takes `isSqlite` ctor arg.
- SQLite: per-conn pragmas in `CreateConnectionAsync`; WAL set once in `InitializeAsync`; `ShutdownAsync` = `wal_checkpoint(TRUNCATE)`; `BackupAsync` = checkpoint + `VACUUM INTO`. MySQL: those three no-ops.
- `OnConfigParsed`: `InitializeAsync` sync before `DatabaseMigrationAsync`. `Unload`: `ShutdownAsync` only when `hotReload == false`, 2s `task.Wait` guard (blocking on hot-reload → watchdog crash).
- Insert script ending `SELECT last_insert_rowid()`/`LAST_INSERT_ID()` → `ExecuteScalarAsync<int?>` + `BeginTransactionAsync`. Only `BanManager.AddBanByIp` does it yet.

## Commands
In `Commands/basecommands.cs`/`basebans.cs`/`basecomms.cs`. New cmd = 3 places: handler (`[CommandHelper]`+`[RequiresPermissions]`), `CommandMappings` in `Commands/RegisterCommands.cs`, default aliases in `RegisterCommands.CreateConfig`.
Fork-specific: `css_sqlbackup` (root, SQLite-only, `<ModuleDir>/backups/`, retain `SqliteBackupRetainCount`=14), `css_clearall`/`css_clearpenalties` (root, one-transaction lift of bans/comms/warns; warns SteamID-only).

## Conventions
- Reply after async DB work → `Server.NextWorldUpdateAsync` before `ReplyToCommand`/entity access.
- Menus: CS2MenuManager NuGet (runtime plugin `CS2MenuManager_MenuManager`). Build via `Helper.CreateMenu(title, backAction)`; admin menus via `Menus.AdminMenuRegistry.Instance.RegisterCategory/RegisterMenu`. No `3rd_party/`.
- NuGet from `packages/cs2menumanager.<ver>.nupkg` (`nuget.config` source mapping). Bump = replace nupkg + `<PackageReference>` version.
- Discord: `Helper.LogCommand` covers standard line; extra `DiscordWebhookClientLog?.SendMessageAsync` only for extra detail.

## CI / Release
- No GitHub Actions (workflows removed, Actions off). Releases built on RE server (`re.miksen.me`, `/root/cs2-build-system`): ops.miksen.me → Releases, or `server-ops release --only CS2-SimpleAdmin [--dry-run]` / `gh workflow run release -R mrc4tt/server-ops -f only=CS2-SimpleAdmin [-f dry_run=false]`. Dry run default.
- `build-all.sh` has a `CS2-SimpleAdmin` case mirroring `package.sh` (zip root `counterstrikesharp/{plugins,shared}`, bundles CS2MenuManager latest or `CS2MM_TAG`). Settings in `plugins.conf [CS2-SimpleAdmin]`. Bumps patch from commit count since last tag, commits `Release vX.Y.Z` (VERSION + CHANGELOG), tags, `gh release create`.
- CHANGELOG: auto section dumps all non-docs/ci/chore subjects under `### Changed`. For admin-only Added/Fixed wording, commit the `## [X.Y.Z] - date` section by hand before releasing; existing section is kept.
- `scripts/` gitignored, local: old `package.sh` / `release.sh` (superseded, don't mix with RE releases).
- Tags `vX.Y.Z`. Update check reads `VERSION` from `raw.githubusercontent.com/mrc4tt/CS2-SimpleAdmin/main`.
- `CHANGELOG.md` = admin-visible behaviour only.
