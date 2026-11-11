---
title: Bans, mutes and warnings
description: How to ban, gag, mute, silence, warn and kick players, offline and by IP, and how to lift penalties again.
sidebar:
  order: 1
---

Every penalty can be given from chat (`!ban`, `/ban`), the client console (`css_ban`), the server
console or RCON, and from the [admin menu](/guides/admin-menu/). This page explains how they work;
[Commands](/reference/commands/) lists every command with its exact usage and flag.

## The penalty types

| Penalty | Blocks | Command | Flag |
| --- | --- | --- | --- |
| **Ban** | Joining the server | `css_ban` | `@css/ban` |
| **Gag** | Text chat (all chat and team chat) | `css_gag` | `@css/chat` |
| **Mute** | Voice chat | `css_mute` | `@css/chat` |
| **Silence** | Text chat and voice | `css_silence` | `@css/chat` |
| **Warning** | Nothing by itself; counts towards [thresholds](#warnings) | `css_warn` | `@css/kick` |
| **Kick** | Removes the player once, nothing is stored as active | `css_kick` | `@css/kick` |

Gags, mutes and silences are stored together (as comm penalties) and come back when the player
reconnects. A gagged player can still type chat commands such as `!penalties`.

## Giving a penalty

```txt
css_ban <#userid or name> [time] [reason]
css_gag <#userid or name> [time] [reason]
css_warn <#userid or name> [time] [reason]
css_kick <#userid or name> [reason]
```

Mute and silence work like gag. Examples:

```txt
css_ban #12 1d Cheating
css_gag "Some Player" 30 Spamming
css_warn #4 0 Admin disrespect
```

- **Target**: a `#userid` (from `status` or `css_players`) or a name. Names with spaces need quotes.
  CounterStrikeSharp's group targets such as `@all`, `@ct` or `@t` also work, but with
  `DisableDangerousCommands` on (the default), ban, warn, gag, mute and silence refuse to hit more
  than one player at once.
- **Immunity**: you cannot penalize a player whose admin immunity is higher than yours.
- **Reason**: everything after the time. Without one it is stored as "Unknown".
- **No time**: in game, a ban, gag, mute or silence without a time opens a menu to pick the
  duration (turn this off with `OtherSettings.ShowBanMenuIfNoTime`). From the server console these
  four need a time. `css_warn` and the `css_add…` commands below without a time are permanent.

### Time format

| Written | Means |
| --- | --- |
| `30` | 30 minutes (a plain number is minutes) |
| `0` | Permanent |
| `30m`, `12h`, `3d`, `2w` | Minutes, hours, days, weeks |
| `1mo`, `1y` | Months (30 days), years (365 days) |
| `1d12h` | Units can be combined |

A time that cannot be read, like `5x` or `soon`, is refused with "Invalid time" and nothing is
applied. A reason without a time in front of it counts as such a time, so write
`css_warn #4 0 Spamming`, not `css_warn #4 Spamming`.

### Limits per admin

Two flags decide how long an admin may penalize, checked for commands and menus alike:

- Without **`@css/permban`** an admin cannot ban or warn permanently, nor longer than
  `OtherSettings.MaxBanDuration` (default 10080 minutes, one week).
- Without **`@css/permmute`** the same applies to gags, mutes and silences, with
  `OtherSettings.MaxMuteDuration` (also one week).

The server console has no limits.

### Time running out

With `OtherSettings.TimeMode` `1` (the default) penalty time runs in real time. With `0` it only
counts down while the player is on the server. Expired penalties are cleared about once a minute.

## Players who are not on the server

The `add` commands take a SteamID64 instead of a player on the server:

```txt
css_addban <steamid64> [time] [reason]
css_addgag <steamid64> [time] [reason]
css_addmute <steamid64> [time] [reason]
css_addsilence <steamid64> [time] [reason]
```

If the player is online, the penalty is applied right away; otherwise it waits for their next
connect.

`css_disconnected` (alias `css_last`, `@css/kick`) opens a menu of the players who left recently,
with their SteamIDs, to ban, gag, mute or silence them without typing the ID. It remembers the last
`OtherSettings.DisconnectedPlayersHistoryCount` players (default 10) until the plugin reloads.

## IP bans

`css_banip <ip> [time] [reason]` (`@css/ban`) bans an IPv4 address. A player on the server with
that IP is banned right away; otherwise the ban is stored for the next connect.

`OtherSettings.BanType` decides whether IPs are checked at all:

| `BanType` | Effect |
| --- | --- |
| `0` | SteamID only. IP bans are not checked, and bans do not store the player's IP. |
| `1` (default) | SteamID and IP. Each ban also stores the player's IP, so a banned player is also blocked from that address on another account. |

When IPs are checked, two more settings matter:

- **`CheckMultiAccountsByIp`** (default on): a joining player is refused when any banned account
  has used the same IP.
- **`IgnoredIps`**: addresses never checked, for example a LAN café or a shared VPN exit.
- **`ExpireOldIpBans`**: number of days after which the IP part of a ban stops counting; `0`
  (default) means never. The SteamID ban is unaffected.

## Warnings

A warning is stored with a duration like any penalty; it becomes inactive when its time runs out,
and a warning with time `0` never expires. Only active warnings count.

`WarnThreshold` in the config runs a server command when a player reaches a number of active
warnings. Placeholders: `USERID` and `STEAMID64`. With the default config the 998th and 999th
warning trigger a ban, so change the numbers to what you use:

```json
"WarnThreshold": {
  "3": "css_addban STEAMID64 60 \"3/4 Warn\"",
  "4": "css_ban #USERID 120 \"4/4 Warn\""
}
```

At or above the highest number its command runs again for every new warning. The warned player is
frozen for a few seconds so they see the message.

## Looking players up

| Command | Flag | Shows |
| --- | --- | --- |
| `css_penalties` (`css_mypenalties`, `css_comms`) | none | A player's own active penalties. With a target (needs `@css/kick`): that player's. |
| `css_who <#userid or name>` | `@css/generic` | In the console: SteamIDs, admin flags, penalty totals, other accounts seen on the same IP, and the IP itself with `@css/showip`. |
| `css_warns <#userid or name>` | `@css/kick` | The player's warnings, in a menu. |
| `css_history <target or steamid64> [bans\|gags\|mutes\|silences\|warns]` | `@css/kick` | Full penalty history, also for offline players. |
| `css_banlist [pattern]` (`css_bans`) | `@css/ban` | Active bans with their `#id`, optionally filtered. |

## Lifting penalties

```txt
css_unban <#id, steamid64, name or ip> [reason]
css_ungag <steamid64 or name> [reason]
css_unmute <steamid64 or name> [reason]
css_unsilence <steamid64 or name> [reason]
css_unwarn <steamid64, or the name or ip of a player on the server>
```

- `css_unban #17` lifts the ban with that id from `css_banlist`. Anything else is matched exactly
  against SteamID64, name **and** IP, and every active ban that matches is lifted. Names must be
  written exactly as stored. The reply says how many bans were lifted, or that none matched.
- `css_unwarn` lifts the player's most recent active warning. A name or IP only works while that
  player is on the server; for offline players use the SteamID64.
- Lifted bans are kept in the history together with who lifted them and why.

### Clearing everything for one player

```txt
css_clearall <steamid64> [reason]
```

`css_clearall` (alias `css_clearpenalties`, `@css/root`) lifts all active bans, gags, mutes,
silences and warnings for a player in one database transaction: either everything is lifted, or
on an error nothing is. It replies with what was cleared, for example
`bans:1 gags:0 mutes:2 silences:0 warns:3`, and posts the same summary to the
[Discord command log](/guides/discord/).

- Bans match on SteamID, name or IP; comm penalties on SteamID or name; **warnings only on the
  SteamID64**. Pass the SteamID64 to be sure everything is found.
- Kicks, admin rights and the penalty history are not touched.
