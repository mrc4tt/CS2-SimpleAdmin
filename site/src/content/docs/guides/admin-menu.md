---
title: Admin menu
description: Opening the admin menu, what is in it, who sees which entries, and how to change durations, reasons and the menu style.
sidebar:
  order: 2
---

Type `!admin` in chat (or `css_admin` in the console) to open the admin menu. It needs
`@css/generic` and only works in game.

## What is in it

The menu shows only what the admin's flags allow: entries without permission are hidden, and a
category with no visible entries is hidden too.

| Category | Entry | Flag |
| --- | --- | --- |
| **Manage players** | Slap, Slay | `@css/slay` |
| | Kick, Warn, Force team, History | `@css/kick` |
| | Ban | `@css/ban` |
| | Gag, Mute, Silence | `@css/chat` |
| **Manage server** | Change map | `@css/changemap` |
| | Restart game | `@css/generic` |
| | Custom commands | `@css/generic`, plus each command's own flag |
| | Manage plugins | `@css/root` |
| **Manage admins** | Add admin, Remove admin, Reload admins | `@css/root` |
| **Fun commands** (module) | God, NoClip, Respawn, Give weapon | `@css/cheats` |
| | Strip, Freeze, HP, Speed, Gravity, Money, Resize | `@css/slay` |

The **Fun commands** category comes from the
[Fun Commands module](/extending/modules/); without it the category is gone. Other modules can add
their own categories.

## Giving a penalty from the menu

**Manage players → Ban → player → duration → reason.** Gag, mute, silence and warn work the same
way; kick skips the duration. The player list leaves out bots and players with higher immunity.

The menu follows the same rules as the commands: without `@css/permban` or `@css/permmute`,
"Permanent" and durations above the maximum are refused (see
[Limits per admin](/guides/penalties/#limits-per-admin)).

## Changing durations and reasons

The lists come from `MenuConfig` in `CS2-SimpleAdmin.json`:

```json
"MenuConfig": {
  "MenuType": "selectable",
  "Durations": [
    { "name": "1 minute", "duration": 1 },
    { "name": "1 day", "duration": 1440 },
    { "name": "Permanent", "duration": 0 }
  ],
  "BanReasons": ["Hacking", "Voice Abuse", "Chat Abuse", "Admin disrespect", "Other"],
  "KickReasons": ["Voice Abuse", "Chat Abuse", "Admin disrespect", "Other"],
  "WarnReasons": ["Voice Abuse", "Chat Abuse", "Admin disrespect", "Other"],
  "MuteReasons": ["Advertising", "Spamming", "Spectator camera abuse", "Hate", "Admin disrespect", "Other"]
}
```

Durations are in minutes, `0` is permanent. `MuteReasons` is used for gags, mutes and silences.
Reasons can only be picked from the list; for anything else use the command with a typed reason.
`AdminFlags` is the list of flags the **Add admin** entry offers.

The map list comes from `DefaultMaps` and `WorkshopMaps`, the custom commands from
`CustomServerCommands` (see [Configuration](/getting-started/configuration/#menu-entries-for-your-own-commands)).

## Menu style

`MenuType` sets how menus look:

| `MenuType` | Style |
| --- | --- |
| `selectable` (default) | Each player picks their own style with `!menutype`. |
| `wasdmenu` | Navigate with W/S, select with E (`wasd` and `dynamic` also work). |
| `center` | HTML menu in the middle of the screen. |
| `chat` | Numbered list in chat, pick with `!1`, `!2`… |
| `console` | Printed in the console. |

`!menutype` (`css_menutype`) belongs to the bundled CS2MenuManager. The player's choice is saved
per SteamID: in CS2MenuManager's own MySQL database if it has one configured, otherwise in a file
under `shared/CS2MenuManager/`.
