---
title: Multiple servers
description: Sharing bans, comm penalties, warnings and admins between several servers through one MySQL database.
sidebar:
  order: 5
---

Several servers can share one ban list and one admin list by using the same **MySQL** database.
SQLite is a file on one machine and cannot be shared. Set up MySQL as described in
[Database](/getting-started/database/#mysql) and give every server the same `DatabaseConfig`.

## How servers are told apart

On start each server registers itself in the `sa_servers` table under its address
(`ip:port`, from the `ip` ConVar or else the server's public IP) and its hostname. The row's `id`
is the server's **server id**, stored with every penalty, admin and group it creates.

Because the address is the key, a server that changes IP or port is registered as a **new** server
with a new id. Penalties are not affected when `MultiServerMode` is on, but admins and groups that
were added only for the old id no longer apply. Re-add them, or update `server_id` in the database.

## MultiServerMode

`MultiServerMode` (on by default) decides whether penalties are shared:

| | `MultiServerMode: true` | `MultiServerMode: false` |
| --- | --- | --- |
| A ban, gag, mute, silence or warning | applies on every server | applies only on the server that gave it |
| Unban, unmute, unwarn, `css_clearall` | lift the penalty everywhere | lift only this server's penalties |
| `css_history`, penalty counts | from all servers | from this server only |

Turn it off when servers share a database only for convenience but should keep separate ban
lists, for example a retake server and a surf server with different staff.

A ban given on one server reaches the others within about a minute: every server refreshes its ban
list from the database every 61 seconds. A banned player who joins another server in that window
is kicked at the next refresh.

## Admins and groups

Admins and groups are scoped by server id **regardless of `MultiServerMode`**:

- `css_addadmin` and `css_addgroup` add the admin or group **for this server only**.
- Add `-g` to make them **global**: they then apply on every server on the database.

  ```txt
  css_addadmin 76561198000000000 Alice #moderator 50 -g
  css_addgroup #moderator @css/generic,@css/kick,@css/ban,@css/chat 50 -g
  ```

- `css_deladmin <steamid64> -g` removes a global admin everywhere.

Other servers do not notice a new admin by themselves. Run `css_reloadadmins` there, restart them,
or turn on `ReloadAdminsEveryMapChange` so each map change reloads the list.

## Announcements across servers

Admin actions ("ADMIN banned Player") are only shown on the server where they happened. The core
plugin has no cross-server messaging. The `CS2-SimpleAdmin_RedisInform` module in the repository
relays these messages through Redis, but it is experimental, not part of the release, and has to be
built from source.
