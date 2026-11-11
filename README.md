<p align="center">
  <a href="https://simpleadmin.miksen.me/">
    <img src="https://img.shields.io/badge/docs-simpleadmin.miksen.me-blue" alt="Documentation" />
  </a>
  <a href="https://github.com/mrc4tt/CS2-SimpleAdmin/releases/latest">
    <img src="https://img.shields.io/github/v/release/mrc4tt/CS2-SimpleAdmin?color=orange&label=release" alt="Latest Release" />
  </a>
</p>

# CS2-SimpleAdmin (Forked)

> **Manage your Counter-Strike 2 server with simple commands!**  
> CS2-SimpleAdmin is a plugin designed to help you easily manage your Counter-Strike 2 server with user-friendly commands. Whether you're banning players, managing teams, or configuring server settings, CS2-SimpleAdmin has you covered.

📘 **Documentation: [simpleadmin.miksen.me](https://simpleadmin.miksen.me/)** — installation, configuration,
guides (bans and mutes, admin menu, Discord, multiple servers), the full command and config reference,
and the module API.

---

## 🚀 Features

- 🎮 **Simple, Intuitive Commands:** Manage players, teams, bans, and server settings using an easy command system.
- 🗄 **Full Database Integration:** Reliable MySQL backend with first-class SQLite support (WAL-mode, busy-timeout, in-game `css_sqlbackup` via `VACUUM INTO`) for persistent data storage.
- ⚡ **Efficient and Lightweight:** Designed to minimize server resource usage while maintaining robust functionality.
- 🚨 **Real-time Notifications:** Instant in-game and Discord notifications to keep admins and players informed.
- 🎛 **User-Friendly Admin Interface:** Plugin menus for quick and efficient management without complex commands.
- 🔄 **Multi-Server Compatibility:** Manage and sync data across multiple servers seamlessly.
- 🧩 **Modular and Extensible:** Tailor the plugin with API access to add custom commands and automation.
- 📜 **Complete Auditing Logs:** Track all administrative actions rigorously for accountability.
- 🌐 **Discord Integration:** Stream logs and alerts to your Discord channels for centralized monitoring.
- 🤝 **Community Driven:** Open-source with ongoing contributions from a passionate community.

---

## ⚙️ Requirements

CS2-SimpleAdmin is fully self-contained — drop the release zip into
`addons/counterstrikesharp/` and you're done. The release ships with:

- [CS2MenuManager](https://github.com/mrc4tt/CS2MenuManager) (bundled - pinned version listed in release notes)
- MySQL **or** SQLite (built-in, see "Choosing a database" below)

---

## 🚀 Getting Started

1. Download the zip from the [latest release](https://github.com/mrc4tt/CS2-SimpleAdmin/releases/latest)
   and extract it into `game/csgo/addons/` (it contains one `counterstrikesharp/` folder).
2. Set `"UnlockConCommands": true` in `addons/counterstrikesharp/configs/core.json`.
3. Start the server, then make yourself an admin from the server console:
   ```
   css_addadmin <your steamid64> <name> @css/root 100
   ```
4. Type `!admin` in game.

The full walkthrough is in [Installation](https://simpleadmin.miksen.me/getting-started/installation/).
Coming from daffyyyy's CS2-SimpleAdmin? See [Switching from daffyyyy's version](https://simpleadmin.miksen.me/getting-started/migrating/).

---

## 📁 Configuration

The configuration file is generated on the first start at
`addons/counterstrikesharp/configs/plugins/CS2-SimpleAdmin/CS2-SimpleAdmin.json`, with command aliases
in `Commands.json` next to it. Every option is described in the
[configuration reference](https://simpleadmin.miksen.me/reference/configuration/), every command in the
[command reference](https://simpleadmin.miksen.me/reference/commands/).

---

## 🗄 Choosing a database

| Scenario | Recommended | Why |
| --- | --- | --- |
| Single-server, small/medium community | **SQLite** | Zero ops, file-based, WAL gives near-MySQL throughput for one server. Pairs well with `css_sqlbackup`. |
| Multiple servers sharing one ban list | **MySQL** | SQLite is local to one machine; cross-server sync requires a real network DB. |
| > ~100k bans/mutes/warns combined | **MySQL** | Hot data still fits SQLite, but operational tooling (replication, point-in-time recovery, dashboards) is much easier on MySQL. |
| Hosted/shared box where you cannot run MySQL | **SQLite** | The `.sqlite` file lives next to the plugin — no extra service to maintain. |

### SQLite operational notes

- The plugin opens the database in WAL mode. Alongside `cs2-simpleadmin.sqlite`, you'll see two sidecar files: `cs2-simpleadmin.sqlite-wal` and `cs2-simpleadmin.sqlite-shm`. **Copy/move all three together** when relocating the database, or run `css_sqlbackup` first (see below) and copy the resulting consolidated file.
- `css_sqlbackup` (root only) writes a timestamped consistent copy to `addons/counterstrikesharp/plugins/CS2-SimpleAdmin/backups/` via `VACUUM INTO`, then prunes older backups beyond the `SqliteBackupRetainCount` config value (default 14). Safe to run while players are online.
- For MySQL, use `mysqldump` — `css_sqlbackup` will refuse and tell you so.

---

## 🛠️ Development

This project started as a base for other plugins but has grown into a standalone admin management tool. Contributions are welcome! If you'd like to help with development or have ideas for new features, feel free to submit a pull request or open an issue.

Writing a module? Start with [Modules](https://simpleadmin.miksen.me/extending/modules/) and the
[API reference](https://simpleadmin.miksen.me/extending/api/).

---

## 💬 Support

Need help, found a bug, or have a feature idea? Join the Discord support server:

[![Discord](https://img.shields.io/badge/Discord-Join%20the%20server-5865F2?logo=discord&logoColor=white)](https://discord.gg/zuQN3U2ZEh)

- **Questions and setup help:** ask in the support channels on Discord.
- **Bug reports:** open a [GitHub issue](https://github.com/mrc4tt/CS2-SimpleAdmin/issues) with your server log, plugin version and steps to reproduce. Discord is fine for a quick check first.
- **Feature requests:** post them on Discord or open an [issue](https://github.com/mrc4tt/CS2-SimpleAdmin/issues).

Please do not send support requests by direct message. Asking in the public channels means others can help and find the answer later.

---

## 🤝 Contributing & Feedback

Help improve CS2-SimpleAdmin by:
- Reporting bugs or requesting features on [GitHub](https://github.com/mrc4tt/CS2-SimpleAdmin/issues)
- Submitting pull requests with improvements
- Participating in discussions and sharing ideas

---

## 💡 Credits

This project is inspired by the work of [Hackmastr](https://github.com/Hackmastr/css-basic-admin/). Thanks for laying the groundwork!

---

## 📄 License
This project is licensed under the GPL-3.0 license - see the [LICENSE](LICENSE) file for details.
