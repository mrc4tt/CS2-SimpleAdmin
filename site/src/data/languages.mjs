// Translations shipped in lang/ folders (the plugin and the Fun Commands module), read at build
// time: language name, number of keys and the keys missing compared with en.json.
import { readdirSync } from 'node:fs';
import { join } from 'node:path';
import { readText } from './csharp.mjs';
import { repoRoot } from './version.mjs';

export const LANG_DIRS = [
	{ id: 'plugin', title: 'CS2-SimpleAdmin', dir: 'CS2-SimpleAdmin/lang' },
	{ id: 'fun', title: 'Fun Commands module', dir: 'Modules/CS2-SimpleAdmin_FunCommands/CS2-SimpleAdmin_FunCommands/lang' },
];

const names = new Intl.DisplayNames(['en'], { type: 'language' });
const label = (code) => {
	try {
		return names.of(code) ?? code;
	} catch {
		return code;
	}
};

export function languages(root = repoRoot()) {
	return LANG_DIRS.map(({ id, title, dir }) => {
		const files = readdirSync(join(root, dir)).filter((f) => f.endsWith('.json')).sort();
		const load = (f) => JSON.parse(readText(join(root, dir, f)));
		const en = Object.keys(load('en.json'));
		const langs = files.map((f) => {
			const code = f.replace(/\.json$/, '');
			const keys = Object.keys(load(f));
			const missing = en.filter((k) => !keys.includes(k));
			return { code, name: label(code), keys: keys.length, missing, file: `${dir}/${f}` };
		});
		langs.sort((a, b) => (a.code === 'en' ? -1 : b.code === 'en' ? 1 : a.name.localeCompare(b.name)));
		return { id, title, dir, total: en.length, langs };
	});
}
