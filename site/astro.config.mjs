// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

// Static site: `npm run build` writes dist/, deploy.sh copies it to the web server.
// No remark plugins: pages that show the version or generated data are .mdx and import it from
// src/data/. (If remark plugins are added, also install @astrojs/markdown-remark: Astro 7's default
// Markdown processor ignores them otherwise.)
export default defineConfig({
	site: 'https://simpleadmin.miksen.me',
	trailingSlash: 'always',
	integrations: [
		starlight({
			title: 'CS2-SimpleAdmin',
			description: 'Admin plugin for Counter-Strike 2 servers running CounterStrikeSharp: bans, gags, mutes, warnings, admin menus and a module API. Maintained fork of daffyyyy/CS2-SimpleAdmin.',
			logo: { src: './src/assets/logo.svg', replacesTitle: false },
			favicon: '/favicon.svg',
			social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/mrc4tt/CS2-SimpleAdmin' }],
			lastUpdated: true,
			disable404Route: true,
			// Source engine .cfg files have no grammar; show them as plain text.
			expressiveCode: { shiki: { langAlias: { cfg: 'txt' } } },
			customCss: ['./src/styles/theme.css'],
			sidebar: [
				{ label: 'Getting started', items: [{ autogenerate: { directory: 'getting-started' } }] },
				{ label: 'Guides', items: [{ autogenerate: { directory: 'guides' } }] },
				{ label: 'Reference', items: [{ autogenerate: { directory: 'reference' } }] },
				{ label: 'Extending', items: [{ autogenerate: { directory: 'extending' } }] },
				{ label: 'Changelog', items: [{ slug: 'whats-new' }, { slug: 'changelog', label: 'Full changelog' }] },
			],
		}),
	],
});
