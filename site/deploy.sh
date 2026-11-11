#!/usr/bin/env bash
# Builds the docs site and publishes it to the RE server (simpleadmin.miksen.me).
#
#   ./deploy.sh                 build, upload as a new release, switch to it, keep the last 5
#   ./deploy.sh --skip-build    upload the dist/ that is already there
#   ./deploy.sh --list          show the releases on the server
#   ./deploy.sh --rollback ID   switch back to an earlier release (an ID from --list)
#
# Every release goes to /srv/simpleadmin-docs/releases/<UTC time>-<commit>/ and nginx serves the
# symlink /srv/simpleadmin-docs/current, so a deploy or a rollback is one symlink swap. Uploads go
# over the normal `ssh re` login (root); the vhost and DNS live in server-ops, see deploy/SETUP.md.
#
# Settings (environment): DEPLOY_HOST (default re, an ssh host or alias), DEPLOY_KEEP (default 5),
# SITE_URL.
set -euo pipefail

cd "$(dirname "$0")"
HOST="${DEPLOY_HOST:-re}"
KEEP="${DEPLOY_KEEP:-5}"
URL="${SITE_URL:-https://simpleadmin.miksen.me/}"
ROOT=/srv/simpleadmin-docs
TARGET="$HOST:$ROOT"
RSYNC=(rsync -e "ssh -o BatchMode=yes")

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

releases() {
	"${RSYNC[@]}" --list-only "$TARGET/releases/" | awk '$NF != "." && $1 ~ /^d/ { print $NF }' | sort
}

current() {
	# -l: list the symlink itself ("current -> releases/X") instead of skipping it.
	"${RSYNC[@]}" -l --list-only "$TARGET/current" | awk '{ print $NF }' | sed 's|.*releases/||; s|/$||'
}

switch_to() {
	# Sync a folder that holds only the link: rsync to "current" itself would follow the old
	# symlink and write the new link inside the old release.
	rm -rf "$tmp/l"
	mkdir "$tmp/l"
	ln -sfn "releases/$1" "$tmp/l/current"
	"${RSYNC[@]}" -rl "$tmp/l/" "$TARGET/"
	echo "current -> releases/$1"
}

case "${1:-}" in
	--list)
		cur="$(current || true)"
		releases | while read -r r; do [ "$r" = "$cur" ] && echo "$r  (current)" || echo "$r"; done
		exit 0 ;;
	--rollback)
		id="${2:?usage: deploy.sh --rollback <id from --list>}"
		releases | grep -qx "$id" || { echo "no release $id on the server" >&2; exit 1; }
		switch_to "$id"
		exit 0 ;;
	--skip-build|"") ;;
	*) sed -n '2,9p' "$0"; exit 2 ;;
esac

if [ "${1:-}" != "--skip-build" ]; then
	# The site shows the newest release tag (src/data/version.mjs). Releases are tagged on the
	# build server, so fetch the tags first; offline, the build uses the tags already here.
	git fetch --tags --quiet 2>/dev/null || echo "note: could not fetch tags, using the local ones" >&2
	npm run build
fi
[ -f dist/index.html ] || { echo "dist/ is empty: build first" >&2; exit 1; }

commit="$(git rev-parse --short HEAD)"
# Everything the build reads: the site itself and the plugin source it generates pages from.
paths=(. ../CHANGELOG.md ../CS2-SimpleAdmin ../CS2-SimpleAdminApi ../Modules)
if ! git diff --quiet HEAD -- "${paths[@]}" || [ -n "$(git status --porcelain -- "${paths[@]}")" ]; then
	commit="$commit-dirty"
fi
id="$(date -u +%Y%m%d-%H%M%S)-$commit"
# The version the site shows: the newest vX.Y.Z tag (same rule as src/data/version.mjs).
version="$(git tag -l 'v*' | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | sort -V | tail -1 | sed 's/^v//')"
[ -n "$version" ] || version="$(tr -d '[:space:]' < ../CS2-SimpleAdmin/VERSION)"

echo "uploading release $id (CS2-SimpleAdmin $version)"
prev="$(current 2>/dev/null || true)"
# Unchanged files are hard links into the previous release, so a release costs only what changed.
link=()
[ -n "$prev" ] && link=(--link-dest="$ROOT/releases/$prev/")
# First deploy: create /srv/simpleadmin-docs/releases (rsync creates only the last level).
ssh -o BatchMode=yes "$HOST" "install -d -m 755 $ROOT $ROOT/releases"
"${RSYNC[@]}" -rlt --chmod=D755,F644 "${link[@]}" dist/ "$TARGET/releases/$id/"
switch_to "$id"

# Keep the newest $KEEP releases (and the one now live): delete the rest by syncing an empty
# folder over releases/ with the kept ones excluded.
mapfile -t all < <(releases)
start=$(( ${#all[@]} > KEEP ? ${#all[@]} - KEEP : 0 ))
keep=("${all[@]:start}")
live="$(current 2>/dev/null || true)"
excludes=(--exclude="/$id/")
[ -n "$live" ] && excludes+=(--exclude="/$live/")
for k in "${keep[@]}"; do excludes+=(--exclude="/$k/"); done
mkdir "$tmp/empty"
"${RSYNC[@]}" -r --delete "${excludes[@]}" "$tmp/empty/" "$TARGET/releases/"
echo "kept ${#keep[@]} release(s)"

# Smoke test through Cloudflare: the page must answer and name this version.
# Fetch first: "curl | grep -q" can fail under pipefail when grep exits early (SIGPIPE).
page="$(curl -fsS --max-time 15 "$URL" 2>/dev/null || true)"
if grep -qF "$version" <<<"$page"; then
	echo "OK: $URL serves $version"
else
	echo "WARNING: $URL does not show $version yet (Cloudflare cache, DNS not switched, or nginx)" >&2
fi
