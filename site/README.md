# CS2-SimpleAdmin documentation site

Astro + Starlight. Served at https://simpleadmin.miksen.me from the RE server (see `deploy/SETUP.md`).

```sh
npm install
npm run dev        # http://localhost:4321
npm run build      # dist/
./deploy.sh        # build and publish (--list, --rollback ID)
```

The build reads the plugin source one folder up, so the reference pages cannot drift:

| What | From | Where |
| --- | --- | --- |
| Version, download link | newest `vX.Y.Z` git tag (fallback `CS2-SimpleAdmin/VERSION`) | `src/data/version.mjs`, `src/data/plugin.ts` |
| Commands | `Commands/RegisterCommands.cs`, handler attributes in `Commands/*.cs`, Fun Commands module | `src/data/commands.mjs` |
| Config options | `CS2-SimpleAdmin/Config.cs` (descriptions: `src/data/config-notes.mjs`) | `src/data/config.mjs` |
| Languages | `lang/*.json` of the plugin and the Fun Commands module | `src/data/languages.mjs` |
| API reference | `CS2-SimpleAdminApi/*.cs` | `src/data/api.mjs` |
| Modules | `Modules/*` | `src/data/modules.mjs` |
| Changelog, What's new | `CHANGELOG.md` | `src/data/changelog.ts` |

The build prints a warning when the source no longer lines up (a config option without a
description, a command mapping without a handler, an API member in no area). Pages are in
`src/content/docs/`; `.mdx` pages import generated data from `src/data/`.
