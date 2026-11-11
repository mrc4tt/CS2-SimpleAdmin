// Small helpers for reading C# source at build time. Not a C# parser: just enough for the shapes
// SimpleAdmin uses (attributes above a method, /// doc comments, auto-properties with defaults).
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';

export const readText = (p) => readFileSync(p, 'utf-8').replace(/^\uFEFF/, '').replace(/\r\n/g, '\n');

/** Lines of a C# file with whole-line // comments blanked (commented-out code is not live). */
export function liveLines(path) {
	return readText(path)
		.split('\n')
		.map((l) => (/^\s*\/\/(?!\/)/.test(l) ? '' : l));
}

/** Text of a /// <summary> block ending just above `index` (attributes in between are skipped). */
export function docAbove(lines, index) {
	let i = index - 1;
	while (i >= 0 && /^\s*\[/.test(lines[i])) i--;
	const doc = [];
	while (i >= 0 && /^\s*\/\/\//.test(lines[i])) doc.unshift(lines[i--].replace(/^\s*\/\/\/\s?/, ''));
	const text = doc.join('\n');
	const m = text.match(/<summary>([\s\S]*?)<\/summary>/);
	return m ? cleanDoc(m[1]) : '';
}

/** Attribute lines ("[X(...)]") directly above `index`, in source order. */
export function attributesAbove(lines, index) {
	const out = [];
	for (let i = index - 1; i >= 0 && /^\s*\[/.test(lines[i]); i--) out.unshift(lines[i].trim());
	return out;
}

/** XML doc text to one line of plain text (<c>x</c> -> `x`, <see cref="X"/> -> X). */
export function cleanDoc(s) {
	return s
		.replace(/<c>(.*?)<\/c>/g, '`$1`')
		.replace(/<see\s+cref="([^"]+)"\s*\/>/g, '`$1`')
		.replace(/<paramref\s+name="([^"]+)"\s*\/>/g, '`$1`')
		.replace(/<[^>]+>/g, '')
		.replace(/\s*\u2014\s*/g, ', ')
		.replace(/\s+/g, ' ')
		.trim();
}

/** The first sentence of a doc summary. */
export const firstSentence = (s) => (s.match(/^.*?[.!?](?=\s|$)/)?.[0] ?? s).trim();

/** Split a C# argument list on top-level commas (respects strings, brackets and parentheses). */
export function splitArgs(s) {
	const out = [];
	let depth = 0, cur = '', str = false;
	for (let i = 0; i < s.length; i++) {
		const c = s[i];
		if (str) {
			cur += c;
			if (c === '\\') cur += s[++i];
			else if (c === '"') str = false;
			continue;
		}
		if (c === '"') { str = true; cur += c; continue; }
		if ('([{'.includes(c)) depth++;
		if (')]}'.includes(c)) depth--;
		if (c === ',' && depth === 0) { out.push(cur.trim()); cur = ''; continue; }
		cur += c;
	}
	if (cur.trim()) out.push(cur.trim());
	return out;
}

/** A C# string literal to its value; anything else unchanged. */
export const unquote = (s) => (/^@?"(.*)"$/s.test(s.trim()) ? s.trim().replace(/^@?"|"$/g, '').replace(/\\"/g, '"') : s.trim());

/** Every file under `dir` with one of the extensions, skipping bin/ and obj/. */
export function walk(dir, exts = ['.cs']) {
	const out = [];
	for (const name of readdirSync(dir)) {
		if (name === 'bin' || name === 'obj' || name.startsWith('.')) continue;
		const p = join(dir, name);
		if (statSync(p).isDirectory()) out.push(...walk(p, exts));
		else if (exts.some((e) => name.endsWith(e))) out.push(p);
	}
	return out.sort();
}

export const rel = (root, p) => relative(root, p).split('\\').join('/');
