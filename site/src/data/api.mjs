// The public API (CS2-SimpleAdminApi/) read at build time: every member of ICS2_SimpleAdminApi
// with its signature and /// summary, plus the types it uses (PenaltyType, PlayerInfo,
// MenuContext...). Signatures are shown as written in the source.
import { join } from 'node:path';
import { cleanDoc, readText, walk, rel } from './csharp.mjs';
import { repoRoot } from './version.mjs';

const API_DIR = 'CS2-SimpleAdminApi';

/** Member declarations of a type body with their doc comments. */
function members(body) {
	const out = [];
	const lines = body.split('\n');
	let doc = [];
	for (let i = 0; i < lines.length; i++) {
		const t = lines[i].trim();
		if (t.startsWith('///')) { doc.push(t.replace(/^\/\/\/\s?/, '')); continue; }
		if (!t || t.startsWith('//') || t.startsWith('[')) { if (!t) doc = doc.length && !t ? doc : []; continue; }
		// A declaration runs until ";" or a "{" (property/ctor body).
		let decl = t;
		while (!/[;{]\s*$/.test(decl) && !/\{\s*get;/.test(decl) && i + 1 < lines.length) decl += ' ' + lines[++i].trim();
		const text = doc.join('\n');
		doc = [];
		if (/^(?:public\s+)?(?:static\s+readonly\s+)?(?:event\s+)?[\w<>?,\s()[\].]+\(?/.test(decl) && /^(public\s|event\s|[\w<>?]+[\s(])/.test(decl)) {
			const summary = cleanDoc(text.match(/<summary>([\s\S]*?)<\/summary>/)?.[1] ?? '')
				.replace(/\p{Extended_Pictographic}\uFE0F?\s*(?:NEW:\s*)?/gu, "")
				.replace(/\s+/g, ' ').trim();
			const params = [...text.matchAll(/<param name="(\w+)">([\s\S]*?)<\/param>/g)].map((m) => ({ name: m[1], text: cleanDoc(m[2]) }));
			out.push({ decl: decl.replace(/\s*\{[\s\S]*$/, '').replace(/;\s*$/, '').replace(/^public\s+/, '').trim(), summary, params });
		}
	}
	return out;
}

/** Kind and name of a member declaration. */
function classify(decl) {
	if (/^event\s/.test(decl)) return { kind: 'event', name: decl.match(/(\w+)\s*$/)[1] };
	if (/^static\s/.test(decl)) return { kind: 'field', name: decl.match(/(\w+)\s*=/)?.[1] ?? decl };
	const m = decl.match(/(\w+)\s*\(/);
	if (m) return { kind: 'method', name: m[1] };
	return { kind: 'property', name: decl.match(/(\w+)\s*$/)?.[1] ?? decl };
}

/** Body of a type ("{ ... }" after its header) from a source string. */
function typeBody(src, header) {
	const start = src.search(header);
	if (start < 0) return null;
	const open = src.indexOf('{', start);
	let depth = 1, i = open + 1;
	for (; i < src.length && depth; i++) {
		if (src[i] === '{') depth++;
		else if (src[i] === '}') depth--;
	}
	return { head: src.slice(start, open).trim(), body: src.slice(open + 1, i - 1) };
}

let cache;
export function api(root = repoRoot()) {
	if (cache) return cache;
	const file = `${API_DIR}/ICS2-SimpleAdminApi.cs`;
	const src = readText(join(root, file));
	const iface = typeBody(src, /public interface ICS2_SimpleAdminApi/);
	if (!iface) throw new Error(`${file}: interface ICS2_SimpleAdminApi not found`);
	const list = members(iface.body).map((m) => ({ ...m, ...classify(m.decl) }));
	const capability = src.match(/PluginCapability<ICS2_SimpleAdminApi\?>\s+PluginCapability\s*=\s*new\("([^"]+)"\)/)?.[1] ?? null;

	// Every other public type in the API project.
	const types = [];
	for (const p of walk(join(root, API_DIR))) {
		const s = readText(p);
		for (const m of s.matchAll(/public\s+(class|enum|record|struct)\s+(\w+)/g)) {
			const t = typeBody(s.slice(m.index), new RegExp(`public\\s+${m[1]}\\s+${m[2]}`));
			if (!t) continue;
			const doc = s.slice(0, m.index).match(/((?:\s*\/\/\/.*\n)+)\s*$/)?.[1] ?? '';
			const summary = cleanDoc(doc.replace(/^\s*\/\/\/\s?/gm, '').match(/<summary>([\s\S]*?)<\/summary>/)?.[1] ?? '');
			const ctor = t.head.match(/\(([\s\S]*)\)/)?.[1];
			const entry = { kind: m[1], name: m[2], summary, file: rel(root, p) };
			if (m[1] === 'enum') {
				entry.values = t.body.split(',').map((v) => v.replace(/\/\/.*$/gm, '').trim()).filter(Boolean);
			} else {
				entry.ctor = ctor ? ctor.split(',').map((x) => x.trim()).filter(Boolean) : null;
				entry.members = members(t.body).map((x) => ({ ...x, ...classify(x.decl) })).filter((x) => x.kind === 'property' || x.kind === 'method');
				// Auto-properties "Type Name { get; ... } = value" -> "Type Name".
				for (const x of entry.members) x.decl = x.decl.replace(/\s*\{.*$/, '');
			}
			types.push(entry);
		}
	}
	return (cache = { file, capability, members: list, types });
}
