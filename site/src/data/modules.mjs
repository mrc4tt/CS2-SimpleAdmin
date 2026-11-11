// The modules in Modules/, read at build time: name, version and author from the plugin class,
// the config class with its defaults, the API members each module uses and any commands it adds
// with [ConsoleCommand]. Which modules ship in the release zip is not in the source tree (the
// release build lives on the build server), so that list is kept here.
import { existsSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { configReference } from './config.mjs';
import { liveLines, readText, rel, walk } from './csharp.mjs';
import { repoRoot } from './version.mjs';

/** Modules the release zip contains (same list as the release build's CS2-SimpleAdmin case). */
export const SHIPPED = ['CS2-SimpleAdmin_FunCommands', 'CS2-SimpleAdmin_StealthModule'];

/** One line on what a module does, for modules without a ModuleDescription (from their code). */
const ABOUT = {
	'CS2-SimpleAdmin_FunCommands': 'Noclip, god mode, freeze, respawn, give and strip weapons, HP, speed, gravity, money and resize, as commands and as a Fun category in the admin menu. The reference module for writing your own.',
	'CS2-SimpleAdmin_StealthModule': 'Hides admins who use css_hide from other players (CheckTransmit) and can hide them on join and block the status command for silent admins.',
	'CS2-SimpleAdmin_BanSoundModule': 'Plays a sound to everyone when a player is banned (OnPlayerPenaltied).',
	'CS2-SimpleAdmin_ExampleModule': 'Example of the API: events, commands and menus. Not meant for a live server.',
	'CS2-SimpleAdmin_RedisInform': 'Shares admin activity messages (bans, gags, mutes, silences) between servers through Redis: each server publishes its own and shows the others.',
	'AntiDLL-CS2-SimpleAdmin': 'Bans players that the AntiDLL plugin detects, through SimpleAdmin (needs AntiDLL).',
};

const field = (src, name) => src.match(new RegExp(`${name}\\s*=>\\s*"([^"]*)"`))?.[1] ?? null;

let cache;
export function modules(root = repoRoot()) {
	if (cache) return cache;
	const dir = join(root, 'Modules');
	const apiMembers = new Set();
	const out = [];
	for (const name of readdirSync(dir).sort()) {
		const path = join(dir, name);
		if (!statSync(path).isDirectory()) continue;
		const files = walk(path);
		const main = files.find((f) => /:\s*BasePlugin\b/.test(readText(f)));
		if (!main) continue;
		const src = readText(main);
		const all = files.map((f) => liveLines(f).join('\n')).join('\n');

		// Config: the class implementing (I)BasePluginConfig, if any.
		let config = null;
		for (const f of files) {
			const cm = readText(f).match(/public\s+(?:sealed\s+)?class\s+(\w+)\s*:\s*I?BasePluginConfig/);
			if (cm) { config = { file: rel(root, f), class: cm[1], ...configReference(rel(root, f), cm[1], root) }; break; }
		}
		// API members used: "_api.X(", "_sharedApi!.X", "SimpleAdminApi.X"...
		const used = [...new Set([...all.matchAll(/(?:_?[sS]haredApi|_?[aA]pi|_simpleAdminApi|SimpleAdminApi)!?\??\.(\w+)/g)].map((m) => m[1]))]
			.filter((m) => m !== 'Get' && /^[A-Z]/.test(m))
			.sort();
		used.forEach((u) => apiMembers.add(u));
		const consoleCommands = [...all.matchAll(/\[ConsoleCommand\("([^"]+)"/g)].map((m) => m[1]);

		out.push({
			dir: name,
			name: field(src, 'ModuleName') ?? name,
			version: field(src, 'ModuleVersion'),
			author: field(src, 'ModuleAuthor'),
			description: field(src, 'ModuleDescription'),
			about: ABOUT[name] ?? null,
			shipped: SHIPPED.includes(name),
			readme: existsSync(join(path, 'README.md')) ? `Modules/${name}/README.md` : null,
			source: rel(root, main),
			config,
			apiUsed: used,
			consoleCommands,
		});
		if (!ABOUT[name]) console.warn(`[modules] ${name}: no description in src/data/modules.mjs`);
	}
	out.sort((a, b) => Number(b.shipped) - Number(a.shipped) || a.dir.localeCompare(b.dir));
	return (cache = { modules: out, apiMembers });
}
