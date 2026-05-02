<p align="center">
  <a href="https://git.miksen.me/mikkel/CS2-SimpleAdmin/actions">
    <img src="https://git.miksen.me/mikkel/CS2-SimpleAdmin/actions/workflows/build.yml/badge.svg" alt="Build and Publish" />
  </a>
  <a href="https://git.miksen.me/mikkel/CS2-SimpleAdmin/releases/latest">
    <img src="https://img.shields.io/gitea/v/release/mikkel/CS2-SimpleAdmin?gitea_url=https%3A%2F%2Fgit.miksen.me&color=orange&label=release" alt="Latest Release" />
  </a>
  <img src="https://img.shields.io/badge/Made_with-a_lot_of_tea_%F0%9F%8D%B5-red" alt="Made with a lot of tea 🍵" />
</p>

# CS2-SimpleAdmin

---

> **Manage your Counter-Strike 2 server with simple commands!**  
> CS2-SimpleAdmin is a plugin designed to help you easily manage your Counter-Strike 2 server with user-friendly commands. Whether you're banning players, managing teams, or configuring server settings, CS2-SimpleAdmin has you covered.

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

- [CS2MenuManager](https://github.com/schwarper/CS2MenuManager) (bundled — pinned version listed in release notes)
- [PlayerSettings](https://github.com/NickFox007/PlayerSettingsCS2) (bundled)
- [AnyBaseLibCS2](https://github.com/NickFox007/AnyBaseLibCS2) (bundled)
- MySQL **or** SQLite (built-in, see "Choosing a database" below)

---

## 🚀 Getting Started

1. **Clone or Download the Repository**:  
   Download or clone the repository and publish to your `addons/counterstrikesharp/plugins/` directory.

2. **First Launch Configuration**:  
   On the first launch, a configuration file will be generated at:
   ```
   addons/counterstrikesharp/configs/plugins/CS2-SimpleAdmin/CS2-SimpleAdmin.json
   ```
   Edit this file to customize the plugin settings according to your server needs.

3. **Enjoy Managing Your Server!**  
   Use the commands provided by the plugin to easily manage your server.

---

## 📁 Configuration

The configuration file (`CS2-SimpleAdmin.json`) will be auto-generated after the first launch. It contains settings for MySQL connections, command permissions, and other plugin-specific configurations.

---

## 🗄 Choosing a database

| Scenario | Recommended | Why |
| --- | --- | --- |
| Single-server, small/medium community | **SQLite** | Zero ops, file-based, WAL gives near-MySQL throughput for one server. Pairs well with `css_sqlbackup`. |
| Multiple servers sharing one ban list | **MySQL** | SQLite is local to one machine; cross-server sync requires a real network DB. |
| > ~100k bans/mutes/warns combined | **MySQL** | Hot data still fits SQLite, but operational tooling (replication, point-in-time recovery, dashboards) is much easier on MySQL. |
| Hosted/shared box where you cannot run MySQL | **SQLite** | The `.sqlite` file lives next to the plugin — no extra service to maintain. |

### SQLite operational notes

- The plugin opens the database in WAL mode. Alongside `cs2-simpleadmin.sqlite` you'll see two sidecar files: `cs2-simpleadmin.sqlite-wal` and `cs2-simpleadmin.sqlite-shm`. **Copy/move all three together** when relocating the database, or run `css_sqlbackup` first (see below) and copy the resulting consolidated file.
- `css_sqlbackup` (root only) writes a timestamped consistent copy to `addons/counterstrikesharp/plugins/CS2-SimpleAdmin/backups/` via `VACUUM INTO`, then prunes older backups beyond the `SqliteBackupRetainCount` config value (default 14). Safe to run while players are online.
- For MySQL, use `mysqldump` — `css_sqlbackup` will refuse and tell you so.

---

## 🛠️ Development

This project started as a base for other plugins but has grown into a standalone admin management tool. Contributions are welcome! If you'd like to help with development or have ideas for new features, feel free to submit a pull request or open an issue.

---

## 🤝 Contributing & Feedback

Help improve CS2-SimpleAdmin by:
- Reporting bugs or requesting features on [Forgejo](https://git.miksen.me/mikkel/CS2-SimpleAdmin/issues)
- Submitting pull requests with improvements
- Participating in discussions and sharing ideas

---

## 💡 Credits

This project is inspired by the work of [Hackmastr](https://github.com/Hackmastr/css-basic-admin/). Thanks for laying the groundwork!

---

## 📄 License
This project is licensed under the GPL-3.0 license - see the [LICENSE](LICENSE) file for details.
