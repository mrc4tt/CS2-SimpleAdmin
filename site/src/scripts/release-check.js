// Release check in the browser: the site is built from the newest git tag at deploy time, so a
// release made after the last deploy would not show until the next one. This asks GitHub for the
// newest release and, when it is newer than the build, updates every element marked with:
//   data-sa-version         text becomes the new version
//   data-sa-download        href becomes the new zip
//   data-sa-version-text    occurrences of the built version in its text are replaced
//   data-sa-unreleased="x"  removed once version x is released ("Not released yet" marks)
// Read-only, no credentials; GitHub's limit is 60 unauthenticated calls per hour per address, so
// the answer is kept in sessionStorage for an hour. Any failure leaves the built page as it is.
const REPO = 'mrc4tt/CS2-SimpleAdmin';
const KEY = 'sa-latest-release';
const MAX_AGE = 60 * 60 * 1000;

const parse = (v) => {
	const m = /^v?(\d+)\.(\d+)\.(\d+)$/.exec(String(v || '').trim());
	return m ? m.slice(1).map(Number) : null;
};
const newer = (a, b) => {
	const x = parse(a), y = parse(b);
	if (!x || !y) return false;
	return x[0] - y[0] > 0 || (x[0] === y[0] && (x[1] - y[1] > 0 || (x[1] === y[1] && x[2] > y[2])));
};
const atLeast = (a, b) => !newer(b, a) && parse(a) && parse(b);

function cached() {
	try {
		const c = JSON.parse(sessionStorage.getItem(KEY) || 'null');
		return c && Date.now() - c.at < MAX_AGE ? c.tag : null;
	} catch {
		return null;
	}
}

async function latestTag() {
	const hit = cached();
	if (hit) return hit;
	const res = await fetch(`https://api.github.com/repos/${REPO}/releases/latest`, { headers: { Accept: 'application/vnd.github+json' } });
	if (!res.ok) return null;
	// Tags are "vX.Y.Z"; the site shows "X.Y.Z".
	const tag = String((await res.json()).tag_name || '').replace(/^v/, '');
	if (!parse(tag)) return null;
	try {
		sessionStorage.setItem(KEY, JSON.stringify({ tag, at: Date.now() }));
	} catch {
		// Storage blocked: just ask again next time.
	}
	return tag;
}

// The version the page was built with: data-sa-built on any element, else nothing to compare.
export async function checkRelease() {
	const built = document.querySelector('[data-sa-built]')?.getAttribute('data-sa-built');
	if (!built) return;
	const marked = document.querySelector('[data-sa-version], [data-sa-download], [data-sa-version-text], [data-sa-unreleased]');
	if (!marked) return;
	let tag;
	try {
		tag = await latestTag();
	} catch {
		return;
	}
	if (!tag) return;
	for (const el of document.querySelectorAll('[data-sa-unreleased]')) {
		if (atLeast(tag, el.getAttribute('data-sa-unreleased'))) el.remove();
	}
	if (!newer(tag, built)) return;
	for (const el of document.querySelectorAll('[data-sa-version]')) el.textContent = tag;
	for (const el of document.querySelectorAll('[data-sa-download]')) el.setAttribute('href', `https://github.com/${REPO}/releases/download/v${tag}/CS2-SimpleAdmin-${tag}.zip`);
	for (const el of document.querySelectorAll('[data-sa-version-text]')) {
		const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
		for (let n = walker.nextNode(); n; n = walker.nextNode()) n.nodeValue = n.nodeValue.split(built).join(tag);
	}
}
