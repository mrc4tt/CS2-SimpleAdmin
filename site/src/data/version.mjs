// The SimpleAdmin version the site shows: the newest release (git tag "vX.Y.Z"), not
// CS2-SimpleAdmin/VERSION. VERSION is bumped by the release build on the RE server in the same
// commit that is tagged, but a checkout can be ahead of the last release, and a download link to an
// untagged version would point at a zip that does not exist. Plain JS: used by the .ts data files,
// the remark plugin and astro.config.mjs.
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';

/** The repository root: the site lives in <repo>/site and is built from there (npm run build). */
export const repoRoot = () => resolve(process.cwd(), '..');

/** "1.9.6", "v1.9.6" or "build-1.9.2" -> [1, 9, 6]; null for anything else. */
export function parseVersion(v) {
	const m = /^(?:v|build-)?(\d+)\.(\d+)\.(\d+)$/.exec(String(v ?? '').trim());
	return m ? m.slice(1).map(Number) : null;
}

/** Negative, 0 or positive like a sort comparator; unparsable versions sort first. */
export function compareVersions(a, b) {
	const x = parseVersion(a) ?? [-1, -1, -1];
	const y = parseVersion(b) ?? [-1, -1, -1];
	return x[0] - y[0] || x[1] - y[1] || x[2] - y[2];
}

/** CS2-SimpleAdmin/VERSION (the next or current release, written by the release build). */
export function sourceVersion(root = repoRoot()) {
	try {
		return readFileSync(join(root, 'CS2-SimpleAdmin/VERSION'), 'utf-8').trim();
	} catch {
		return 'dev';
	}
}

/**
 * The newest release as "x.y.z" (without the "v"). Only "vX.Y.Z" tags count: those are the
 * releases with a CS2-SimpleAdmin-X.Y.Z.zip. Falls back to VERSION without git or tags.
 */
export function releasedVersion(root = repoRoot()) {
	try {
		const tags = execFileSync('git', ['-C', root, 'tag', '-l', 'v*'], { encoding: 'utf-8' })
			.split('\n')
			.map((t) => t.trim())
			.filter((t) => /^v\d+\.\d+\.\d+$/.test(t));
		if (tags.length) return tags.sort(compareVersions).at(-1).slice(1);
	} catch {
		// No git (for example a build from a source archive): fall through.
	}
	return sourceVersion(root);
}
