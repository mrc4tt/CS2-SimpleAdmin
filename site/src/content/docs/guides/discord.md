---
title: Discord webhooks
description: Sending the admin command log and penalty embeds to Discord channels.
sidebar:
  order: 4
---

CS2-SimpleAdmin can post to Discord through webhooks. There are two kinds:

- **Command log**: one line of text for every admin command, sent to one webhook.
- **Penalty embeds**: a card for each ban, mute, gag, silence and warning. Each penalty type has
  its own webhook, so bans can go to one channel and warnings to another.

Both are off until you set a webhook URL. In Discord, open the channel's settings, then
**Integrations → Webhooks → New Webhook**, and copy its URL.

## Command log

```json
"Discord": {
  "DiscordLogWebhook": "https://discord.com/api/webhooks/..."
}
```

Each admin command is posted as one line, for example:

> **AdminName** issued command `css_ban #3 60 cheating` on server `My CS2 Server`

The admin name links to their Steam profile; commands from the server console show as Console.
The text comes from `sa_discord_log_command` in the plugin's `lang/` files, where `HOSTNAME` is
replaced with the server's hostname and `ADDRESS` with its `ip:port`.

What gets logged:

- every command that goes through the plugin's command log, including unban, unmute, ungag and
  unwarn (those have no embed);
- actions picked in the admin menu and its custom commands list;
- `css_clearall`, with a summary of how many penalties it lifted;
- any extra command names you list in `OtherSettings.AdditionalCommandsToLog`, also commands that
  belong to other plugins.

## Penalty embeds

Each penalty type has a list of `name` / `value` settings:

```json
"Discord": {
  "DiscordPenaltyBanSettings": [
    { "name": "Color", "value": "#E74C3C" },
    { "name": "Webhook", "value": "https://discord.com/api/webhooks/..." },
    { "name": "ThumbnailUrl", "value": "" },
    { "name": "ImageUrl", "value": "" },
    { "name": "Footer", "value": "My community" },
    { "name": "Time", "value": "{relative}" }
  ]
}
```

The same block exists as `DiscordPenaltyMuteSettings`, `DiscordPenaltyGagSettings`,
`DiscordPenaltySilenceSettings` and `DiscordPenaltyWarnSettings`. A type with an empty `Webhook`
sends nothing.

| Setting | Effect |
| --- | --- |
| `Webhook` | The webhook URL for this penalty type. Empty = no embeds for this type. |
| `Color` | The embed's side color as hex, with or without `#`. Empty = white. An invalid value such as `#E74C3G` logs a warning and also uses white. |
| `ThumbnailUrl` | Small image in the top right corner. Optional. |
| `ImageUrl` | Large image under the fields. Optional. |
| `Footer` | Footer text. Optional. |
| `Time` | `{relative}` (the default) shows when the penalty ends as a Discord relative time ("in 2 hours"). Any other value shows the length instead ("2h 5m"). Permanent penalties always show "Permanent". |

The embed itself is fixed:

- **Title**: "Ban registered", "Mute registered" and so on.
- **Description**: the server's hostname.
- **Fields**: Player (linked to their Steam profile), SteamID (hidden behind a spoiler), Duration,
  Reason and Admin.
- **Timestamp**: when the penalty was given.

Titles and field names come from the plugin's `lang/` files, in the server's language. Lifting a
penalty sends no embed; it shows in the command log.

## When nothing arrives

- Check the server console. A webhook that answers with an error is logged with Discord's reply,
  for example a deleted webhook (`404`) or rate limiting (`429`). Failed posts are not retried.
- Discord limits each webhook to a few messages per second. On a busy server, give the command log
  and the penalty embeds separate webhooks.
- The config is read when the plugin loads: after editing the webhooks, restart the server or run
  `css_plugins reload CS2-SimpleAdmin`.
