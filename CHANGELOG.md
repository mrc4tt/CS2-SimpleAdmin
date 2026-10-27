# Changelog

Notable changes for server admins. Internal refactors, CI and build tweaks are left out.

Releases are tagged `vX.Y.Z` (older releases used `build-X.Y.Z`).

## [1.9.5] - 2026-10-27

### Changed

- Update README.md
- Update README.md
- Update README.md

## [1.9.4] - 2026-10-24

### Fixed
- The admin notice on player connect always showed 0 bans, gags, mutes and silences. It now shows the player's real penalty history.

## [1.9.3] - 2026-10-22

### Fixed
- Handle SDR/P2P players without IP (show as "SDR relay")

## [1.9.2] - 2026-10-09

### Fixed
- SourceTV is no longer ban-checked or saved as a player on join.
- Every join was ban-checked, IP-saved and logged twice; now once.
- Unauthenticated players (SteamID still 0) no longer get an IP/player record written.
- The per-join `[BAN CHECK]` log line is now Debug level.

## [1.9.1] - 2026-10-07

### Fixed
- `no such table: sa_renames` after updating only `CS2-SimpleAdmin.dll`. Migrations are now embedded in the DLL, so DLL-only updates still apply them.
- A missing `sa_renames` table is recreated even if the migration was already recorded.

## [1.9.0] - 2026-10-03

### Added
- `css_history <#userid|name|steamid64> [bans|gags|mutes|silences|warns]` (`@css/kick`, alias `css_penaltyhistory`): full penalty history with status, duration, admin, reason and who lifted it. Works for offline players by SteamID64. Also under Players > History in the admin menu.
- Renames from `css_prename` are stored in the database (`sa_renames`) and survive restarts and reconnects.

### Fixed
- Unbans take effect immediately instead of after the ~60s cache refresh. SQLite unbans and expiries are now picked up by the cache.
- Admin loading no longer relies on a fixed 3s startup delay, and concurrent reloads can't race. A database error no longer wipes `admins.json`.
- `css_say` shows the admin name (`sa_adminsay_prefix` takes `{1}`).
- `css_penalties <player>` shows the target's penalties instead of the caller's.
- `css_rename` works for silent admins.
- `css_slay` and the slay menus ignore dead/spectator targets (could crash the server).

## [1.8.3] - 2026-07-18

### Added
- `wasdmenu` MenuType (`wasd`/`dynamic` kept as aliases).

### Fixed
- A player's WasdMenu choice is honored instead of being downgraded to the center HTML menu.
- Player-target menus (Kick/Ban/Slay/...) were empty after `css_plugins restart`.
- Plugins Manager menu title, and it now has a back button.
- `css_respawn` on a spectator/unassigned player crashed the server.
