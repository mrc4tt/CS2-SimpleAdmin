# Hosting simpleadmin.miksen.me on the RE server

The site is a static build served by nginx on RE, Cloudflare-proxied like `bins.miksen.me` and
`img.miksen.me`. Everything on the server side lives in
[server-ops](https://github.com/mrc4tt/server-ops):

| What | Where in server-ops |
| --- | --- |
| DNS record (proxied A to RE) and the name on the `miksen-public` certificate | `simpleadmin` in `PROXIED_NAMES`, `etc/domain.conf` |
| nginx vhost | `etc/nginx/simpleadmin.miksen.me.conf` (installed through `manifest`) |

`simpleadmin.miksen.me.conf` in this folder is a copy for reference; the one in server-ops is the
one that is installed.

Layout on the server:

```
/srv/simpleadmin-docs/
  releases/20261111-120000-6cea1a7/   one folder per deploy (the last 5 are kept)
  current -> releases/20261111-120000-6cea1a7
```

## One-time setup

1. Push the server-ops change to `main`. `repo-autosync` applies it within a few minutes (or run
   `server-ops sync`): the vhost is installed and enabled once its certificate exists.
2. On RE as root, add the DNS record and the name to the certificate with the Cloudflare token
   that is already on the server:

   ```sh
   awk -F' = ' '/dns_cloudflare_api_token/ {print $2}' /etc/letsencrypt/cloudflare.ini | server-ops domain
   openssl x509 -in /etc/letsencrypt/live/miksen-public/fullchain.pem -noout -ext subjectAltName
   ```

3. First upload, from your machine:

   ```sh
   cd site && ./deploy.sh
   ```

The upload uses your normal `ssh re` login (root), so no extra key or user is needed.

## Everyday use

```sh
cd site
./deploy.sh                 # build + publish
./deploy.sh --skip-build    # publish the dist/ already built
./deploy.sh --list          # releases on the server
./deploy.sh --rollback ID   # back to an earlier one
```

The script ends with a smoke test through Cloudflare that the page shows the current release.
