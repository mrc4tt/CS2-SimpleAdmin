// Config options read from the config classes at build time: every property with its JSON name,
// type and default value (the C# initializer evaluated to JSON). The descriptions are written by
// hand in config-notes.mjs; the build warns when an option has no description or a description
// names an option that no longer exists, so the two cannot drift apart unnoticed.
import { join } from 'node:path';
import { readText, splitArgs } from './csharp.mjs';
import { repoRoot } from './version.mjs';

/** The classes in one .cs file: { Name: { props: [{ name, json, type, init }] } }. */
export function parseClasses(src) {
	const classes = {};
	const re = /public\s+(?:sealed\s+)?class\s+(\w+)[^{]*\{/g;
	let m;
	while ((m = re.exec(src))) {
		// The class body: from "{" to its matching "}".
		let depth = 1, i = re.lastIndex;
		for (; i < src.length && depth; i++) {
			if (src[i] === '{') depth++;
			else if (src[i] === '}') depth--;
		}
		const body = src.slice(re.lastIndex, i - 1);
		classes[m[1]] = { props: parseProps(body) };
	}
	return classes;
}

function parseProps(body) {
	const props = [];
	// [JsonPropertyName("X")] (optional) public [override] Type Name { get; set; } [= init;]
	const re = /(?:\[JsonPropertyName\("([^"]+)"\)\]\s*)?public\s+(?:override\s+)?(?:required\s+)?([\w<>\[\],?\s]+?)\s+(\w+)\s*\{\s*get;\s*(?:set|init);\s*\}/g;
	let m;
	while ((m = re.exec(body))) {
		let init = null;
		const rest = body.slice(re.lastIndex);
		const eq = rest.match(/^\s*=/);
		if (eq) {
			// The initializer runs to the ";" at depth 0.
			let depth = 0, str = false, j = eq[0].length;
			for (; j < rest.length; j++) {
				const c = rest[j];
				if (str) { if (c === '\\') j++; else if (c === '"') str = false; continue; }
				if (c === '"') str = true;
				else if ('([{'.includes(c)) depth++;
				else if (')]}'.includes(c)) depth--;
				else if (c === ';' && depth === 0) break;
			}
			init = rest.slice(eq[0].length, j).trim();
		}
		props.push({ name: m[3], json: m[1] ?? m[3], type: m[2].replace(/\s+/g, ''), init });
	}
	return props;
}

const elementType = (t) => t.match(/^(?:List<(.+)>|(.+)\[\])\??$/)?.slice(1).find(Boolean) ?? null;
const dictTypes = (t) => t.match(/^Dictionary<(.+?),(.+)>\??$/)?.slice(1) ?? null;

/** Evaluates a C# initializer to a JSON value, using the class table for new() objects. */
export function evaluate(init, type, classes) {
	const s = init?.trim();
	const cls = classes[type.replace(/\?$/, '')];
	if (s == null) return cls ? defaults(type, classes) : /^(int|long|double|float)\??$/.test(type) ? 0 : type === 'bool' ? false : /^string/.test(type) ? null : null;
	if (/^@?"/.test(s)) return JSON.parse(s.replace(/^@/, ''));
	if (s === 'true' || s === 'false') return s === 'true';
	if (s === 'null') return null;
	if (/^[\d\s()+\-*/.]+$/.test(s)) return Function(`"use strict"; return (${s});`)();
	if (s.startsWith('[')) {
		const el = elementType(type) ?? 'object';
		return splitArgs(s.slice(1, -1)).filter(Boolean).map((x) => evaluate(x, el, classes));
	}
	const nw = s.match(/^new(?:\s+[\w<>,\s]+)?\(\)\s*(?:\{([\s\S]*)\})?$/);
	if (nw) {
		const inner = nw[1]?.trim() ?? '';
		const dict = dictTypes(type);
		if (dict) {
			const out = {};
			for (const entry of splitArgs(inner).filter(Boolean)) {
				const [k, v] = splitArgs(entry.replace(/^\{|\}$/g, ''));
				out[String(evaluate(k, dict[0], classes))] = evaluate(v, dict[1], classes);
			}
			return out;
		}
		if (elementType(type)) return inner ? splitArgs(inner).map((x) => evaluate(x, elementType(type), classes)) : [];
		if (cls) {
			const obj = defaults(type, classes);
			for (const a of splitArgs(inner).filter(Boolean)) {
				const [, name, val] = a.match(/^(\w+)\s*=\s*([\s\S]+)$/) ?? [];
				const p = cls.props.find((x) => x.name === name);
				if (p) obj[p.json] = evaluate(val, p.type, classes);
			}
			return obj;
		}
		return {};
	}
	throw new Error(`config: cannot evaluate initializer "${s}" (${type})`);
}

/** The default JSON object of a class (properties in declaration order, like the serializer). */
export function defaults(type, classes) {
	const cls = classes[type.replace(/\?$/, '')];
	if (!cls) throw new Error(`config: unknown class ${type}`);
	const obj = {};
	for (const p of cls.props) obj[p.json] = evaluate(p.init, p.type, classes);
	return obj;
}

/** Friendly type names for the reference table. */
export function typeLabel(t, classes) {
	t = t.replace(/\?$/, '');
	if (classes[t]) return 'object';
	const el = elementType(t);
	if (el) return `list of ${classes[el] ? 'objects' : typeLabel(el, classes)}`;
	const d = dictTypes(t);
	if (d) return `map ${typeLabel(d[0], classes)} to ${typeLabel(d[1], classes)}`;
	return { int: 'number', long: 'number', double: 'number', bool: 'true/false', string: 'text' }[t] ?? t;
}

/**
 * Options of a root config class, one section per nested object, in file order:
 * { defaults, sections: [{ key, title, options: [{ key, path, type, default }] }] }
 */
export function configReference(file, rootClass, root = repoRoot()) {
	const classes = parseClasses(readText(join(root, file)));
	if (!classes[rootClass]) throw new Error(`${file}: class ${rootClass} not found`);
	const all = defaults(rootClass, classes);
	const top = { key: '', title: 'General', options: [] };
	const sections = [top];
	for (const p of classes[rootClass].props) {
		const nested = classes[p.type.replace(/\?$/, '')];
		if (nested) {
			sections.push({
				key: p.json,
				title: p.json,
				options: nested.props.map((q) => ({ key: q.json, path: `${p.json}.${q.json}`, type: typeLabel(q.type, classes), default: all[p.json][q.json] })),
			});
		} else {
			top.options.push({ key: p.json, path: p.json, type: typeLabel(p.type, classes), default: all[p.json] });
		}
	}
	return { defaults: all, sections: sections.filter((s) => s.options.length) };
}
