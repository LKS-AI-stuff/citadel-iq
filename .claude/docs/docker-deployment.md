# Deploying CitadelIQ with Docker Compose

`docker-compose.yml` runs the whole stack on one host:

| Service | Image | Reachable from | Purpose |
|---|---|---|---|
| `db` | `pgvector/pgvector:pg17` (pinned) | the `api` container only | PostgreSQL + pgvector |
| `api` | built from `CitadelIQ.Api/Dockerfile` | the `ui` container only | ASP.NET Core API (runs in Production) |
| `ui` | built from `citadel-iq-ui/Dockerfile` (nginx) | the host, port `UI_PORT` (default `8080`) | Serves the SPA and proxies `/api`, `/auth`, `/signin-oidc` and `/signout-callback-oidc` to the API on the same origin |

Only the UI port is published. The API and Postgres are never exposed to the host.

> **Not yet verified:** these Docker files haven't been built and run end to end. Expect to fix small issues on the
> first run.

## Prerequisites

- Docker with the Compose plugin on the server.
- A domain name pointing at the server, and TLS for it (see step 1).
- The Microsoft Entra External ID tenant, app registration and user flow from the README's
  [Sign-in with Microsoft Entra External ID](../../README.md#-sign-in-with-microsoft-entra-external-id) section.
  You need the **tenant ID**, the **tenant subdomain** (the `citadeliq` in `citadeliq.ciamlogin.com`) and a registered app.
- An OpenAI API key.

## 1. HTTPS and a public address

Pick the public address, e.g. `https://docs.example.com`. HTTPS is required, not optional:

- the session cookie is `Secure`, so browsers only send it over HTTPS (localhost aside);
- Entra accepts plain `http` redirect URIs only for `localhost`.

Put a TLS terminator in front of the `ui` container: Caddy, Traefik, or a cloud load balancer / Application Gateway
forwarding to `UI_PORT`. Two things the terminator **must** do:

- send `X-Forwarded-Proto: https` (most do by default). nginx passes it on to the API. Without it the API builds
  `http://` redirect URIs, which Entra rejects.
- keep the original `Host` header (`docs.example.com`), which the API also uses to build the redirect URIs.

If you terminate TLS on nginx itself instead, add a `listen 443 ssl` server to `citadel-iq-ui/nginx.conf` and publish
port 443. No forwarded headers are needed then.

## 2. Redirect URIs in Entra

In the external tenant, either add the production URIs to the existing app registration or (cleaner) create a separate
registration such as `CitadelIQ (production)` and link it to the same `SignUpSignIn` user flow (**User flows →
SignUpSignIn → Applications → Add application**). A separate registration keeps production's secret and URIs apart
from local development.

Add these as **Web** redirect URIs (**Authentication**):

- `https://docs.example.com/signin-oidc`
- `https://docs.example.com/signout-callback-oidc`

If you created a new registration, repeat the README's steps for it: leave the implicit-grant boxes unchecked, grant
the `openid`/`profile`/`email` permissions, and add the optional `email` ID-token claim.

## 3. A production client secret

**Certificates & secrets → New client secret**, e.g. `production`. Copy the **Value**; it's shown only once. Note the
expiry date in your calendar: when the secret expires, nobody can sign in until you create a new one and update
`.env`.

## 4. Configure `.env`

From the repository root:

```bash
cp .env.example .env
```

`.env` is read by `docker compose` and must never be committed. Fill in:

| Variable | Required | Value |
|---|---|---|
| `POSTGRES_PASSWORD` | yes | Password of the database owner (`citadeliq`), used only for migrations |
| `APP_DB_PASSWORD` | yes | Password of the restricted runtime role `citadeliq_app` the API connects as |
| `OPENAI_API_KEY` | yes | Your OpenAI key |
| `OIDC_AUTHORITY` | yes | `https://<subdomain>.ciamlogin.com/<tenant-id>/v2.0` |
| `OIDC_CLIENT_ID` | yes | The registration's **Application (client) ID** |
| `OIDC_CLIENT_SECRET` | yes | The secret **Value** from step 3 |
| `OPENAI_EMBEDDING_MODEL` | no | Default `text-embedding-3-small` |
| `OPENAI_CHAT_MODEL` | no | Chat model for Ask answers |
| `UI_PORT` | no | Host port nginx listens on; default `8080` |
| `STORAGE_PROVIDER` and `AZURE_STORAGE_*` | no | Store files in Azure Blob Storage instead of the `documents` volume (README, Azure Blob section) |
| `COMPOSE_SUBNET` / `UI_PROXY_IP` | no | Only if the default network `172.28.0.0/16` clashes with one on the host (step 7) |
| `EMAIL_AGENT_MODE` | no | `Off` (default), `DraftsOnly` or `On` — the email questions agent (README, email section) |
| `EMAIL_SUPPORT_ADDRESS`, `EMAIL_PORTAL_BASE_URL` | if the agent is on | The shared support address, and the public origin used for links in replies (e.g. `https://docs.example.com`) |
| `EMAIL_IMAP_HOST`, `EMAIL_SMTP_HOST`, `EMAIL_USERNAME`, `EMAIL_PASSWORD` | if the agent is on | The support mailbox; empty `EMAIL_IMAP_HOST` leaves the agent unwired |
| `EMAIL_TRUSTED_AUTHSERV_ID` | if the agent is on | The mail host's receiving server in `Authentication-Results` (e.g. `mx.google.com`) |
| `EMAIL_SENT_FOLDER`, `EMAIL_APPEND_TO_SENT` | no | Sent folder name; `true` if the host doesn't save SMTP-sent mail itself |

Use passwords without `$`, spaces, quotes or `;`. They are embedded in connection strings.

Compose refuses to start if any required variable is missing.

## 5. Start the stack

```bash
docker compose up -d --build
docker compose ps                # all three services up; db "healthy"
docker compose logs -f api       # watch migrations and startup
```

On start the API:

1. applies the database migrations as the owner (`Database__MigrateOnStartup=true`), which creates the tables,
   row-level security policies and the grants for `citadeliq_app`;
2. checks that `citadeliq_app` is not a superuser and can't bypass row-level security, and refuses to start otherwise;
3. uses OIDC sign-in. The development login is refused in Production.

Compose creates three named volumes:

| Volume | Holds |
|---|---|
| `pgdata` | The database |
| `documents` | Uploaded files (unused when `STORAGE_PROVIDER=AzureBlob`) |
| `dpkeys` | The data-protection keys that encrypt session cookies, so restarts and rebuilds don't sign users out |

### The `citadeliq_app` role

`deploy/postgres/initdb/01-app-role.sh` creates `citadeliq_app` with `APP_DB_PASSWORD`, but Postgres runs init scripts
**only when the `pgdata` volume is first created**. Two cases where you need to act:

- **Reusing an existing `pgdata` volume:** create the role by hand, then restart the API:

  ```bash
  docker compose exec db psql -U citadeliq -d citadeliq -c \
    "CREATE ROLE citadeliq_app LOGIN PASSWORD '<APP_DB_PASSWORD>' NOSUPERUSER NOBYPASSRLS;"
  docker compose restart api
  ```

  A volume created before workspaces were added also needs to be recreated: the initial migration was rewritten in
  place, so the old schema won't be upgraded.
- **Changing `APP_DB_PASSWORD` later:** editing `.env` doesn't change the role. Run
  `ALTER ROLE citadeliq_app PASSWORD '<new>';` the same way, then `docker compose up -d`.

## 6. Smoke test

1. Open `https://docs.example.com` and click **Sign in or create an account**. You should be sent to
   `<subdomain>.ciamlogin.com`.
2. Create an account (**No account? Create one**), confirm the emailed code, set a password and display name.
3. You return to onboarding. Create an organization or individual workspace, upload a file and search it.
4. **Sign out** from the account menu goes through Entra and back to the sign-in page.
5. `docker compose restart api`, then reload: you should still be signed in (the `dpkeys` volume works).

nginx doesn't proxy `/health`, so check it from inside the network if needed:
`docker compose exec ui wget -qO- http://api:8080/health`.

**Email agent (if configured):** `docker compose exec ui wget -qO- http://api:8080/health/email` shows the mode and the
last mailbox poll. The API container needs outbound access to the mail host (IMAP 993, SMTP 587). Start with
`EMAIL_AGENT_MODE=DraftsOnly`, email a question from a registered account's address, and check the draft appears under
**Email review**. Only the agent's operators should be able to open the support mailbox: it holds every organization's
incoming questions.

**Trying it on your own machine first:** set `UI_PORT=8080`, register `http://localhost:8080/signin-oidc` and
`http://localhost:8080/signout-callback-oidc` as redirect URIs, and open `http://localhost:8080` in Chrome or Firefox.
Entra allows plain `http` for `localhost`, and those browsers accept the Secure cookie there.

## 7. Network and proxy settings

The API trusts forwarded headers only from nginx. So nginx has a fixed address on a fixed Compose network:

- network `COMPOSE_SUBNET`, default `172.28.0.0/16`
- nginx's address `UI_PROXY_IP`, default `172.28.0.10`, which is also passed to the API as
  `ForwardedHeaders__KnownProxies__0`

If `docker compose up` fails with a "Pool overlaps" error, or the subnet clashes with your LAN or VPN, pick another
range and an address inside it:

```dotenv
COMPOSE_SUBNET=172.31.0.0/16
UI_PROXY_IP=172.31.0.10
```

## 8. Backups

- **`pgdata` and `documents`:** back them up **together**, so database rows and stored files match. With
  `STORAGE_PROVIDER=AzureBlob`, back up the database and turn on soft delete / versioning on the blob container
  instead.
- **`dpkeys`:** optional. Losing it only signs everyone out; no data is lost.
- `docker compose down` keeps the volumes. **`docker compose down -v` deletes them all**, including the database.

A simple database dump:

```bash
docker compose exec -T db pg_dump -U citadeliq -d citadeliq -Fc > citadeliq-$(date +%F).dump
```

## 9. Updating

```bash
git pull
docker compose up -d --build
```

New migrations run automatically when the API starts. Keep `pgvector/pgvector:pg17` pinned: a new Postgres major
version won't start on an existing data volume without a dump and restore.

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| Entra **AADSTS50011** (redirect URI mismatch) | The URI the API sent isn't registered, often `http://` instead of `https://` | Register the exact `https://<host>/signin-oidc`; make sure the TLS terminator sends `X-Forwarded-Proto: https` and keeps `Host` |
| Sign-in returns to the sign-in page, or "Correlation failed" | The Secure cookie was dropped because the site is served over plain `http` | Serve it over HTTPS (step 1) |
| API exits: "…superuser or has BYPASSRLS…" | The runtime connection uses the wrong role | Check `APP_DB_PASSWORD` and that `citadeliq_app` exists (step 5) |
| API exits: password authentication failed for `citadeliq_app` | The role doesn't exist (reused volume) or its password differs from `.env` | Create or `ALTER` the role (step 5) |
| API exits: "Authentication:Oidc:… is required" | An `OIDC_*` value is empty | Fill in `.env`, then `docker compose up -d` |
| Lands on `/?signin=failed` | Entra returned an error | `docker compose logs api`; usually a wrong or expired client secret, or a wrong Authority |
| Everyone signed out after a rebuild | The `dpkeys` volume was removed | Expected after `down -v`; users just sign in again |
| "Pool overlaps with other one on this address space" | Subnet clash | Set `COMPOSE_SUBNET` / `UI_PROXY_IP` (step 7) |
| Uploads over ~50 MB fail at nginx | `client_max_body_size 50m` in `nginx.conf` | The API's own limit is `Upload:MaxFileSizeMB` (20); raise both together |
