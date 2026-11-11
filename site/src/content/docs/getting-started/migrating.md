---
title: Switching from daffyyyy's version
description: Moving a server from the original daffyyyy/CS2-SimpleAdmin to this fork, keeping the database, config and admins.
sidebar:
  order: 6
---

This fork continues [daffyyyy/CS2-SimpleAdmin](https://github.com/daffyyyy/CS2-SimpleAdmin), which is
no longer maintained. It split off at upstream **1.7.9a**. Your database, config, admins and
penalties carry over; the main change is the menu library.

:::caution[Version numbers]
Upstream later released its own 1.8.x and 1.9.x versions. They are **not** the same as this fork's
1.9.x: compare features with the [changelog](/changelog/), not version numbers.
:::

## What carries over

- **Database**: the schema is the same, migrations are tracked in the same `sa_migrations` table,
  and the fork runs only what is missing, on MySQL and SQLite alike. Bans, comm penalties,
  warnings, admins and groups stay.
- **Config**: `CS2-SimpleAdmin.json` is compatible (same `ConfigVersion`, no renamed or removed
  keys). New options, such as `SqliteBackupRetainCount`, use their defaults until you add them.
- **Command aliases**: `Commands.json` is kept; new commands like `css_sqlbackup`, `css_clearall`
  and `css_history` are added to it automatically.
- **Modules**: the API is unchanged (namespace `CS2_SimpleAdminApi`, capability `simpleadmin:api`),
  so modules written against upstream keep working.

## What changes

- **Menus** use [CS2MenuManager](https://github.com/mrc4tt/CS2MenuManager), bundled in the
  release zip, instead of NickFox's MenuManager. The plugin no longer needs `MenuManagerCore`,
  `PlayerSettings`, `MenuManagerApi`, `PlayerSettingsApi` or `AnyBaseLib`.
- **Menu style per player** is now chosen with `!menutype`. Choices players made in the old
  PlayerSettings are not carried over.
- **.NET**: the fork targets .NET 10, so CounterStrikeSharp must be a build that runs .NET 10.
- **Update check** (`EnableUpdateCheck`) now compares against this fork's releases.
- **No statistics call**: upstream's `EnableMetrics` sent the server's address and hostname to
  `api.daffyy.dev` on every start. The fork removed it; the key can stay in your config, it is
  ignored.
- **Unused config key**: `Discord.DiscordAssociatedAccountsSettings` was never read and is gone; it
  is ignored if still in your file.

## Steps

1. Back up the database: `mysqldump` for MySQL, or copy `cs2-simpleadmin.sqlite` together with
   its `-wal` and `-shm` files while the server is stopped.
2. Stop the server.
3. Delete `addons/counterstrikesharp/plugins/CS2-SimpleAdmin/` and
   `addons/counterstrikesharp/shared/CS2-SimpleAdminApi/`. Keep the SQLite file aside if you use
   SQLite. Configs under `configs/plugins/` stay where they are.
4. Remove the old menu dependencies if no other plugin of yours uses them:
   `plugins/MenuManagerCore/`, `plugins/PlayerSettings/`, `shared/MenuManagerApi/`,
   `shared/PlayerSettingsApi/` and `shared/AnyBaseLib/`.
5. Install the fork as in [Installation](/getting-started/installation/), and put the SQLite file
   back into `plugins/CS2-SimpleAdmin/`.
6. Start the server and watch the console: the missing migrations run, then the plugin loads its
   admins.

## Worth checking afterwards

- Modules that used NickFox's MenuManager directly, not through the SimpleAdmin API, need that
  library themselves or an update to CS2MenuManager.
- On MySQL, migration 018 adds the indexes upstream's migration 016 left commented out. On a large
  database the first start after updating can take a little longer while they are built.
