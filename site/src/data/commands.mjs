// Every command, read from the source at build time so the reference cannot drift:
//   - core commands: CommandMappings (name -> handler) and the default aliases in CreateConfig,
//     both in CS2-SimpleAdmin/Commands/RegisterCommands.cs, plus the [CommandHelper] and
//     [RequiresPermissions] attributes and the /// summary on each handler in Commands/*.cs;
//   - module commands: the Fun Commands module registers its commands through the API
//     (RegisterCommand(name, description, handler)) with names from its Config.cs.
// Groups follow the files the code keeps them in. Anything that does not line up (a mapping
// without a handler, an alias entry without a mapping) is returned in `notes` and printed
// as a build warning instead of being dropped silently.
import { join } from 'node:path';
import { attributesAbove, docAbove, firstSentence, liveLines, readText, rel, splitArgs, unquote } from './csharp.mjs';
import { repoRoot } from './version.mjs';

/** Group titles by source file. A new file gets a title from its name and a build note. */
const GROUPS = {
	'basebans.cs': { id: 'bans', title: 'Bans and warnings' },
	'basecomms.cs': { id: 'comms', title: 'Gags, mutes and silences' },
	'basechat.cs': { id: 'chat', title: 'Admin chat' },
	'playercommands.cs': { id: 'players', title: 'Player management' },
	'basecommands.cs': { id: 'admin', title: 'Admin and server' },
	'basevotes.cs': { id: 'votes', title: 'Votes' },
	'Commands.cs': { id: 'fun', title: 'Fun commands (module)' },
};

const WHO = { CLIENT_AND_SERVER: 'both', CLIENT_ONLY: 'client', SERVER_ONLY: 'server' };
const FUN_DIR = 'Modules/CS2-SimpleAdmin_FunCommands/CS2-SimpleAdmin_FunCommands';

/** [CommandHelper(...)] and [RequiresPermissions(...)] from attribute lines. */
function parseAttributes(attrs) {
	const out = { minArgs: 0, usage: '', who: 'both', permissions: [], anyOf: false };
	for (const a of attrs) {
		const helper = a.match(/^\[CommandHelper\((.*)\)\]$/);
		if (helper) {
			// CommandHelper(int minArgs = 0, string usage = "", CommandUsage whoCanExecute = CLIENT_AND_SERVER)
			const names = ['minArgs', 'usage', 'whoCanExecute'];
			splitArgs(helper[1]).forEach((arg, i) => {
				const named = arg.match(/^(\w+)\s*:\s*([\s\S]+)$/);
				const key = named ? named[1] : names[i];
				const val = named ? named[2] : arg;
				if (key === 'minArgs') out.minArgs = Number(val);
				else if (key === 'usage') out.usage = unquote(val);
				else if (key === 'whoCanExecute') out.who = WHO[val.replace(/^CommandUsage\./, '')] ?? 'both';
			});
		}
		const perm = a.match(/^\[(RequiresPermissions(?:Or)?)\((.*)\)\]$/);
		if (perm) {
			out.permissions.push(...splitArgs(perm[2]).map(unquote));
			out.anyOf = perm[1] === 'RequiresPermissionsOr';
		}
	}
	return out;
}

/**
 * The handler's /// summary as an admin-facing line: drops the developer framing the summaries
 * start with ("Handles the 'ban' command, allowing admins to ...") and keeps the rest verbatim.
 */
export function tidySummary(s) {
	const out = s
		.replace(/^(?:Handles|Processes|Executes) the '?\w+'? command,?\s*(?:that |which |allowing admins to )?/i, '')
		.replace(/^Handles the command that /i, '')
		.replace(/^Handles the (\w+) command\s*-\s*/i, '');
	return out ? out[0].toUpperCase() + out.slice(1) : s;
}

/** Handler methods "void Name(CCSPlayerController? caller, CommandInfo ...)" in one file. */
function handlers(root, file) {
	const lines = liveLines(join(root, file));
	const out = new Map();
	lines.forEach((line, i) => {
		const m = line.match(/^\s*(?:public|private|internal)\s+void\s+(\w+)\(\s*CCSPlayerController\?\s+\w+,\s*CommandInfo\s+\w+\)/);
		if (!m) return;
		out.set(m[1], {
			...parseAttributes(attributesAbove(lines, i)),
			description: tidySummary(firstSentence(docAbove(lines, i))),
			source: `${file}:${i + 1}`,
		});
	});
	return out;
}

function coreCommands(root, notes) {
	const regFile = 'CS2-SimpleAdmin/Commands/RegisterCommands.cs';
	const reg = liveLines(join(root, regFile)).join('\n');
	const mappings = [...reg.matchAll(/new\("(css_\w+)",\s*CS2_SimpleAdmin\.Instance\.(\w+)\)/g)].map((m) => ({ name: m[1], method: m[2] }));
	const aliases = new Map(
		[...reg.matchAll(/\{\s*"(css_\w+)",\s*new Command\s*\{\s*Aliases\s*=\s*\[([^\]]*)\]/g)].map((m) => [m[1], splitArgs(m[2]).map(unquote)]),
	);
	if (!mappings.length) throw new Error(`${regFile}: no CommandMappings found (did the format change?)`);

	const files = ['basebans.cs', 'basecomms.cs', 'basechat.cs', 'playercommands.cs', 'basecommands.cs', 'basevotes.cs'];
	const byMethod = new Map();
	for (const f of files) for (const [name, h] of handlers(root, `CS2-SimpleAdmin/Commands/${f}`)) byMethod.set(name, { ...h, file: f });

	const out = [];
	for (const { name, method } of mappings) {
		const h = byMethod.get(method);
		if (!h) { notes.push(`${name}: handler ${method} not found in Commands/*.cs`); continue; }
		out.push({ name, aliases: (aliases.get(name) ?? [name]).filter((a) => a !== name), file: h.file, ...h, module: null });
	}
	for (const key of aliases.keys()) {
		if (!mappings.some((m) => m.name === key)) notes.push(`${key}: has default aliases in RegisterCommands.CreateConfig but no CommandMappings entry in the core plugin (it may come from a module)`);
	}
	return out;
}

function funCommands(root, notes) {
	const cfg = readText(join(root, FUN_DIR, 'Config.cs'));
	const lists = new Map([...cfg.matchAll(/List<string>\s+(\w+)\s*\{[^}]*\}\s*=\s*\[([^\]]*)\]/g)].map((m) => [m[1], splitArgs(m[2]).map(unquote)]));
	const main = liveLines(join(root, FUN_DIR, 'CS2-SimpleAdmin_FunCommands.cs')).join('\n');
	const regs = [...main.matchAll(/foreach\s*\(var command in Config\.(\w+)\)\s*\{\s*_sharedApi\.RegisterCommand\(command,\s*"([^"]*)",\s*(\w+)\)/g)];
	const hs = handlers(root, `${FUN_DIR}/Commands.cs`);
	const out = [];
	for (const [, list, desc, method] of regs) {
		const names = lists.get(list);
		const h = hs.get(method);
		if (!names?.length) { notes.push(`Fun Commands: Config.${list} has no default names`); continue; }
		if (!h) { notes.push(`Fun Commands: handler ${method} not found`); continue; }
		out.push({
			name: names[0], aliases: names.slice(1), file: 'Commands.cs', ...h,
			description: h.description || `${desc}.`, module: 'CS2-SimpleAdmin_FunCommands', configKey: list,
		});
	}
	if (!out.length) notes.push('Fun Commands: no RegisterCommand calls found (did the format change?)');
	return out;
}

let cache;
/** { groups: [{ id, title, file, commands }], all, notes } */
export function commands(root = repoRoot()) {
	if (cache) return cache;
	const notes = [];
	const core = coreCommands(root, notes);
	const fun = funCommands(root, notes);
	// An alias entry for a command a module provides (css_resize) is expected, not a problem.
	const all = [...core, ...fun];
	for (let i = notes.length - 1; i >= 0; i--) {
		const name = notes[i].match(/^(css_\w+): has default aliases/)?.[1];
		if (name && fun.some((c) => c.name === name)) notes.splice(i, 1);
	}
	const groups = [];
	for (const c of all) {
		const g = GROUPS[c.file] ?? { id: c.file.replace(/\.cs$/, '').toLowerCase(), title: c.file };
		if (!GROUPS[c.file]) notes.push(`${c.file}: no group title in src/data/commands.mjs`);
		let group = groups.find((x) => x.id === g.id);
		if (!group) groups.push((group = { ...g, file: c.file, commands: [] }));
		group.commands.push(c);
	}
	const order = Object.values(GROUPS).map((g) => g.id);
	groups.sort((a, b) => (order.indexOf(a.id) + 1 || 99) - (order.indexOf(b.id) + 1 || 99));
	for (const g of groups) g.commands.sort((a, b) => a.name.localeCompare(b.name));
	for (const n of notes) console.warn(`[commands] ${n}`);
	return (cache = { groups, all, notes });
}

/** Admin flags with the commands that need them (from the same data). */
export function flags(root = repoRoot()) {
	const map = new Map();
	for (const c of commands(root).all) {
		for (const p of c.permissions.length ? c.permissions : ['(none)']) {
			if (!map.has(p)) map.set(p, []);
			map.get(p).push(c.name);
		}
	}
	return [...map].sort(([a], [b]) => a.localeCompare(b)).map(([flag, names]) => ({ flag, commands: names.sort() }));
}

export { rel };
