// CHANGELOG.md parsed at build time. It stays the source: the release build on the RE server keeps
// or writes the "## [X.Y.Z] - date" section and the GitHub release links to it.
// Format (Keep a Changelog):
//   # Changelog
//   intro paragraphs
//   ## [1.9.6] - 2026-11-08
//   ### Fixed
//   - item (continuation lines indented)
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { compareVersions, releasedVersion, repoRoot } from './version.mjs';

const root = repoRoot();

/** The newest release (git tag); sections for later versions are not released yet. */
export const latestRelease = releasedVersion(root);
export const isUnreleased = (v: string) => v === 'Unreleased' || compareVersions(v, latestRelease) > 0;

/** Anchor for a version: "1.9.6" -> "196" (GitHub's anchor for "## [1.9.6] - date" is longer). */
export const versionId = (v: string) => v.replace(/\./g, '').toLowerCase();

// The section kinds, in display order. Anything else is shown under its own heading text.
export const KINDS = ['Added', 'Changed', 'Fixed', 'Removed', 'Security', 'Deprecated'] as const;
export const kindClass = (k: string) => (KINDS as readonly string[]).includes(k) ? k.toLowerCase() : 'other';

// ---------- inline Markdown (code, bold, italic, links) ----------

const esc = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

/** Renders one line of Markdown to HTML. */
export function inline(md: string) {
	const parts = md.split(/(`+)([^`]*?)\1/);
	let html = '';
	for (let i = 0; i < parts.length; i++) {
		if (i % 3 === 1) continue; // the backtick run
		if (i % 3 === 2) { html += `<code>${esc(parts[i])}</code>`; continue; }
		html += esc(parts[i])
			.replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, (_, t, u) => `<a href="${u.replace(/&amp;/g, '&')}">${t}</a>`)
			.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
			.replace(/(^|[^*\w])\*([^*\s][^*]*)\*/g, '$1<em>$2</em>');
	}
	return html;
}

export interface Section { kind: string; items: string[] }
export interface Release { version: string; date: string; sections: Section[]; count: number }

let cache: { intro: string[]; releases: Release[] } | null = null;

export function changelog() {
	if (cache) return cache;
	const lines = readFileSync(join(root, 'CHANGELOG.md'), 'utf-8').replace(/\r\n/g, '\n').split('\n');
	const intro: string[] = [];
	const releases: Release[] = [];
	let cur: Release | null = null;
	let sec: Section | null = null;
	let para = '';
	for (const raw of lines) {
		const line = raw.trimEnd();
		const ver = line.match(/^## \[([^\]]+)\](?:\s*-\s*(.+))?\s*$/);
		if (ver) {
			cur = { version: ver[1], date: ver[2]?.trim() ?? '', sections: [], count: 0 };
			releases.push(cur);
			sec = null;
			continue;
		}
		if (/^# /.test(line)) continue; // the "# Changelog" title
		if (!cur) {
			// Intro: paragraphs separated by blank lines.
			if (line) para += (para ? ' ' : '') + line;
			else if (para) { intro.push(para); para = ''; }
			continue;
		}
		const kind = line.match(/^###\s+(.+)$/);
		if (kind) { sec = { kind: kind[1].trim(), items: [] }; cur.sections.push(sec); continue; }
		if (!line.trim()) continue;
		const item = line.match(/^[-*]\s+(.*)$/);
		if (item) {
			if (!sec) { sec = { kind: 'Changed', items: [] }; cur.sections.push(sec); }
			sec.items.push(item[1]);
			cur.count++;
			continue;
		}
		if (/^\s+\S/.test(line) && sec?.items.length) { sec.items[sec.items.length - 1] += ' ' + line.trim(); continue; }
		throw new Error(`CHANGELOG.md: unexpected line under ${cur.version}: ${line}`);
	}
	if (para) intro.push(para);
	for (const r of releases) r.sections.sort((a, b) => order(a.kind) - order(b.kind));
	return (cache = { intro, releases });
}

const order = (k: string) => {
	const i = (KINDS as readonly string[]).indexOf(k);
	return i < 0 ? 99 : i;
};
