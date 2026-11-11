// Values read from the plugin source at build time, so the site always matches this checkout:
// the version (newest vX.Y.Z tag), the landing page numbers and the build requirements.
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { releasedVersion, repoRoot, sourceVersion } from './version.mjs';
import { commands } from './commands.mjs';
import { configReference } from './config.mjs';
import { languages } from './languages.mjs';
import { modules } from './modules.mjs';

const root = repoRoot();
const read = (p: string) => readFileSync(join(root, p), 'utf-8');

export const version = releasedVersion(root);
export const sourceVersionNumber = sourceVersion(root);
export const repo = 'https://github.com/mrc4tt/CS2-SimpleAdmin';
export const upstream = 'https://github.com/daffyyyy/CS2-SimpleAdmin';
export const tag = `v${version}`;
export const zipName = `CS2-SimpleAdmin-${version}.zip`;
export const download = `${repo}/releases/download/${tag}/${zipName}`;

const csproj = read('CS2-SimpleAdmin/CS2-SimpleAdmin.csproj');
const pkg = (name: string) => csproj.match(new RegExp(`<PackageReference Include="${name.replace(/\./g, '\\.')}" Version="([^"]+)"`))?.[1] ?? '?';
/** Build requirements from the csproj and the plugin's load checks (Variables.cs). */
export const requirements = {
	dotnet: csproj.match(/<TargetFramework>net([\d.]+)<\/TargetFramework>/)?.[1] ?? '?',
	css: pkg('CounterStrikeSharp.API'),
	menuManager: pkg('CS2MenuManager'),
	requiredPlugins: [...(read('CS2-SimpleAdmin/Variables.cs').match(/_requiredPlugins\s*=\s*\[([^\]]*)\]/)?.[1] ?? '').matchAll(/"([^"]+)"/g)].map((m) => m[1]),
	requiredShared: [...(read('CS2-SimpleAdmin/Variables.cs').match(/_requiredShared\s*=\s*\[([^\]]*)\]/)?.[1] ?? '').matchAll(/"([^"]+)"/g)].map((m) => m[1]),
};

const cfg = configReference('CS2-SimpleAdmin/Config.cs', 'CS2_SimpleAdminConfig', root);
const langs = languages(root)[0];
export const stats = {
	commands: commands(root).all.length,
	settings: cfg.sections.reduce((n, s) => n + s.options.length, 0),
	languages: langs.langs.length,
	modules: modules(root).modules.length,
};
