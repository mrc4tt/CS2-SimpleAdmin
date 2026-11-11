---
title: Fun commands and stealth
description: The two modules in the release zip, Fun Commands and Stealth, with their commands, flags and config.
sidebar:
  order: 6
---

The release zip contains two [modules](/extending/modules/) next to the plugin. Both are optional:
to leave one out, delete its folder under `addons/counterstrikesharp/plugins/`. Each module has its
own config under `configs/plugins/<ModuleName>/`.

## Fun Commands

`CS2-SimpleAdmin_FunCommands` adds commands for changing players in game, and a **Fun Commands**
category in the [admin menu](/guides/admin-menu/) with the same actions.

| Command | Flag | Does |
| --- | --- | --- |
| `css_noclip <#userid or name>` | `@css/cheats` | Turns noclip on or off. |
| `css_god <#userid or name>` | `@css/cheats` | Turns god mode on or off: damage is cancelled and health refilled. |
| `css_respawn <#userid or name>` | `@css/cheats` | Respawns a player at the spot where they died. Only for players on T or CT. |
| `css_give <#userid or name> <weapon>` | `@css/cheats` | Gives an item, for example `ak47`, `weapon_awp`, `deagle` or `hegrenade`; the `weapon_` / `item_` prefix is optional. An unknown name does nothing. |
| `css_strip <#userid or name>` | `@css/slay` | Removes all weapons. |
| `css_freeze <#userid or name> [seconds]` | `@css/slay` | Freezes the player in place; with a time, unfreezes after that many seconds. |
| `css_unfreeze <#userid or name>` | `@css/slay` | Ends a freeze. |
| `css_hp <#userid or name> <hp>` | `@css/slay` | Sets health. |
| `css_speed <#userid or name> <speed>` | `@css/slay` | Sets movement speed: `1` is normal, `2` twice as fast, `0.5` half. |
| `css_gravity <#userid or name> <gravity>` | `@css/slay` | Sets gravity: `1` is normal, `0.5` half. |
| `css_money <#userid or name> <money>` | `@css/slay` | Sets money. |
| `css_resize <#userid or name> <size>` | `@css/slay` | Scales the player model: `1` is normal size. |

- Targets work as in the plugin: `#userid`, a name, or group targets such as `@ct`. Immunity is
  respected. Except for `css_respawn` and `css_money`, the player has to be alive.
- God mode, speed and gravity end when the player dies, disconnects or a new round starts. Running
  `css_god` or `css_noclip` again turns them off; `css_speed <target> 1` and
  `css_gravity <target> 1` reset.
- Freezing from the admin menu has no time: unfreeze with `css_unfreeze`.
- Each action is announced like an admin action of the plugin (following `ShowActivityType`,
  not shown while the admin is [hidden](#stealth)) and written to the command log and the
  [Discord command log](/guides/discord/).

### Config

`configs/plugins/CS2-SimpleAdmin_FunCommands/CS2-SimpleAdmin_FunCommands.json` lists the command
names for every action:

```json
{
  "NoclipCommands": ["css_noclip"],
  "GodCommands": ["css_god"],
  "FreezeCommands": ["css_freeze"],
  "UnfreezeCommands": ["css_unfreeze"],
  "RespawnCommands": ["css_respawn"],
  "GiveCommands": ["css_give"],
  "StripCommands": ["css_strip"],
  "HpCommands": ["css_hp"],
  "SpeedCommands": ["css_speed"],
  "GravityCommands": ["css_gravity"],
  "MoneyCommands": ["css_money"],
  "ResizeCommands": ["css_resize"]
}
```

- Add names to a list for aliases, for example `["css_noclip", "css_nc"]`.
- An empty list (`[]`) turns the action off: no command and no admin menu entry. Unfreeze has no
  menu entry of its own.

The flags are fixed in the module. To change who may use a command, use a CounterStrikeSharp
permission override in `admin_overrides.json`; it also applies to the matching admin menu entry.

Texts come from the module's own `lang/` folder, in each player's language.

## Stealth

Admins with `@css/kick` can hide themselves with the plugin's own command:

```txt
css_hide
```

`css_hide` (alias `css_stealth`) only works in game. A hidden admin is killed if alive, moved out of
the teams, and then:

- their admin actions are not announced to players (the [command log](/guides/discord/) still
  records them);
- with the Stealth module installed, other players do not see them in the spectator list or as
  an observer.

Run `css_hide` again to come back ("You aren't hidden now!"). Joining a team, spectators included,
also ends hiding.

`css_hidecomms` (`@css/kick`) is unrelated to hiding: it turns off, for you, the chat notice
with a joining player's penalty totals. Run it again to get them back.

### Stealth module

`CS2-SimpleAdmin_StealthModule` builds on `css_hide`:

- **Hidden from spectators**: a hidden admin's observer is not sent to other players.
- **Quiet join and leave**: for hidden admins and for every admin with one of the `Permissions`,
  the team change and disconnect messages are not shown, nor their deaths while not on a team.
- **Hide on join**: admins with one of the `Permissions` run `css_hide` automatically a second
  after joining. They still need `@css/kick` for it to work.
- **Status**: `BlockStatusCommand` hides hidden admins from the `status` command through the
  `mm_excludeslot` command of the separate **StatusBlocker** Metamod plugin. StatusBlocker is not
  in the release zip; without it this setting does nothing.

`configs/plugins/CS2-SimpleAdmin_StealthModule/CS2-SimpleAdmin_StealthModule.json`:

```json
{
  "Permissions": ["@css/ban"],
  "BlockStatusCommand": true,
  "HideAdminsOnJoin": true
}
```

| Setting | Default | Effect |
| --- | --- | --- |
| `Permissions` | `["@css/ban"]` | Admins with any of these flags join hidden and quietly. |
| `HideAdminsOnJoin` | `true` | Run `css_hide` for those admins when they join. |
| `BlockStatusCommand` | `true` | Remove hidden admins from `status` (needs StatusBlocker). |
