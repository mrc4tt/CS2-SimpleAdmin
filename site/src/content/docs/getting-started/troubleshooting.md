---
title: Troubleshooting
description: Common errors in the server console and what to do about them.
sidebar:
  order: 5
---

The plugin logs to the server console and to CounterStrikeSharp's log files
(`addons/counterstrikesharp/logs/`). Most problems show up there on start.

## The plugin unloads right after loading

**`Plugin directory 'CS2MenuManager_MenuManager' missing`** or **`Shared library directory
'CS2-SimpleAdminApi' missing`**: the plugin checks for these folders and unloads itself without
them. Extract the whole release zip, not only the `CS2-SimpleAdmin` folder. See
[Installation](/getting-started/installation/).

**`Problem with database connection!`** followed by the error: the database could not be opened.

- MySQL: check host, port, user, password and database name, that the user may connect from the
  game server's address, and `DatabaseSSlMode` (try `none` on a local server without TLS).
- SQLite: check that the server user can write to `plugins/CS2-SimpleAdmin/`.

**`You need to setup MySQL credentials in config!`**: `DatabaseType` is MySQL but host, user or
database name is empty.

**`You need to specify SQLite file path in config!`**: `SqliteFilePath` is empty.

## Banned players can rejoin for a minute

The console shows **`'UnlockConCommands' is disabled in core.json`**. Set
`"UnlockConCommands": true` in `addons/counterstrikesharp/configs/core.json` and restart.

## Menus do not open or look wrong

- The menus need `CS2MenuManager_MenuManager` from the release zip.
- With `MenuType` `selectable` each player chooses their style with `css_menutype`; set a fixed
  `MenuType` (`wasdmenu`, `center`, `chat`, `console`) to force one.

## Penalty times look wrong

- On MySQL, set `Timezone` to the zone your database times should use. An unknown zone logs
  `Time zone '...' not found. Returning UTC time.`
- With `TimeMode` `0`, penalties only count down while the player is online: a one-day ban lasts
  until the player has spent a day on the server.

## Database errors after an update

`no such table` errors after updating only the DLL were fixed in 1.9.1: migrations are now
embedded in the DLL and run on start, and a missing table is recreated. If you still see one,
look for `Error applying migration` further up in the log; it names the migration that failed.

## Still stuck

Ask on the [Discord support server](https://discord.gg/zuQN3U2ZEh), or open a
[GitHub issue](https://github.com/mrc4tt/CS2-SimpleAdmin/issues) with the server log, the plugin
version and the steps to reproduce. Please do not send support requests by direct message.
