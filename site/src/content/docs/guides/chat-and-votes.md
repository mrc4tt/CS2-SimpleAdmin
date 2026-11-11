---
title: Admin chat and votes
description: Talking to admins and players with the chat commands and @ shortcuts, and running quick votes.
sidebar:
  order: 3
---

## Chat shortcuts

Start a chat message with `@`:

| Typed in | By | Goes to |
| --- | --- | --- |
| All chat (`y`) | an admin with `@css/chat` | Everyone, as an admin announcement (same as `css_say`). |
| Team chat (`u`) | an admin with `@css/chat` | Only admins with `@css/chat`, marked **(ADMIN)**. |
| Team chat (`u`) | a player | Admins with `@css/chat` and the player, marked **(PLAYER)**. A way for players to call an admin. |

A player without `@css/chat` who types `@` in all chat just sends a normal message.

## Chat commands

All need `@css/chat`.

| Command | Does |
| --- | --- |
| `css_say <message>` | Announcement in chat to everyone, with the admin's name shown according to `ShowActivityType`. |
| `css_asay <message>` | Message only admins with `@css/chat` see. |
| `css_psay <#userid or name> <message>` | Private message to a player. |
| `css_csay <message>` | Text in the middle of everyone's screen. |
| `css_hsay <message>` | HUD alert for everyone. |
| `css_cssay <message>` | Chat message without prefix; color tags like `{red}` work. |

The look of admin chat comes from `sa_adminsay_prefix`, `sa_adminchat_template_admin` and
`sa_adminchat_template_player` in the plugin's `lang/` files.

## Listening in

`css_adminvoice` (alias `css_listenall`, `@css/chat`) lets an admin hear the voice chat of both
teams. Run it again to turn it off.

- `css_adminvoice muteAll` mutes the voice of every player without `@css/chat`, for example during
  an announcement.
- `css_adminvoice unmuteAll` gives everyone their voice back, except players with an active mute or
  silence.

## Votes

```txt
css_vote <question> <answer 1> <answer 2> [more answers...]
```

`css_vote` needs `@css/generic`. Put a question or answer with spaces in quotes:

```txt
css_vote "Next map?" de_dust2 de_mirage de_inferno
```

Every player gets a menu with the answers for 30 seconds. After 30 seconds the results are posted
in chat, with the number of votes per answer. A new vote replaces one that is still running, and a
map change cancels it. Nothing happens automatically with the result; the admin acts on it.
