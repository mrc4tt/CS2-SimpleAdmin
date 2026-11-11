---
title: Database
description: Choosing SQLite or MySQL, setting up MySQL, backups and migrations.
sidebar:
  order: 3
---

CS2-SimpleAdmin keeps admins, groups, bans, comm penalties, warnings, player IPs and renames in a
database. SQLite is the default and needs no setup; MySQL is for networks.

## Which one

| Scenario | Use | Why |
| --- | --- | --- |
| One server, small or medium community | **SQLite** | No extra service, a file next to the plugin. WAL mode gives plenty of speed for one server, and `css_sqlbackup` makes backups in game. |
| Several servers sharing one ban list | **MySQL** | SQLite is local to one machine; sharing needs a network database. |
| More than about 100k bans, mutes and warnings combined | **MySQL** | SQLite still copes, but replication, point-in-time recovery and dashboards are easier with MySQL. |
| Hosted box where you cannot run MySQL | **SQLite** | Nothing to install or maintain. |

## SQLite

The default config already uses it:

```json
"DatabaseConfig": {
  "DatabaseType": "SQLite",
  "SqliteFilePath": "cs2-simpleadmin.sqlite",
  "SqliteBackupRetainCount": 14
}
```

The path is relative to `addons/counterstrikesharp/plugins/CS2-SimpleAdmin/`.

- The database runs in WAL mode, so next to `cs2-simpleadmin.sqlite` you will see
  `cs2-simpleadmin.sqlite-wal` and `cs2-simpleadmin.sqlite-shm`. **Copy or move all three
  together**, or run `css_sqlbackup` first and copy the backup.
- `css_sqlbackup` (`@css/root`) writes a consistent, timestamped copy to
  `plugins/CS2-SimpleAdmin/backups/` with `VACUUM INTO`, then deletes the oldest backups beyond
  `SqliteBackupRetainCount`. It is safe while players are online.
- Times are stored in UTC; `Timezone` does not apply.

## MySQL

Create an empty database and a user with rights on it, then set:

```json
"DatabaseConfig": {
  "DatabaseType": "MySQL",
  "DatabaseHost": "127.0.0.1",
  "DatabasePort": 3306,
  "DatabaseUser": "simpleadmin",
  "DatabasePassword": "change-me",
  "DatabaseName": "simpleadmin",
  "DatabaseSSlMode": "preferred"
}
```

- `DatabaseHost`, `DatabaseUser` and `DatabaseName` are required: if one is empty the plugin stops
  with "You need to setup MySQL credentials in config!".
- The plugin creates its tables on the first start.
- Every server that should share bans and admins points at the same database. Keep
  `MultiServerMode` on to apply penalties on all of them; each server registers itself in
  `sa_servers`, and admins or groups added without `-g` only apply on the server they were added on.
- Back up with `mysqldump`. `css_sqlbackup` refuses on MySQL.
- `Timezone` sets the time zone for the times written to MySQL.

## Migrations

Table changes ship as numbered migrations for both databases (`Database/Migrations/Mysql` and
`Sqlite`). They run on every start, before anything else touches the database, and the applied
ones are recorded in `sa_migrations`. Since 1.9.1 the migrations are also embedded in
`CS2-SimpleAdmin.dll`, so updating only the DLL still applies them.

If the connection fails, the plugin logs "Problem with database connection!" with the error and
unloads itself.
