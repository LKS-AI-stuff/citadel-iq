# CitadelIQ

An AI-powered document management and semantic search portal — built to demonstrate the
embeddings → chunking → vector-similarity pipeline behind semantic search, wrapped in a
professional, enterprise-style UI. This is deliberately **not** a chatbot: there's no AI-generated
answer yet, just ranked, relevant document chunks returned for a natural-language query.

## 🎯 What it does

Upload documents, organize them into folders, and search their contents using natural language —
even when your search terms don't literally appear in the text. For example, a document containing:

> "Employees are eligible for 15 days of annual vacation."

is found by searching:

> "How many vacation days can an employee take?"

because the search matches on **meaning** (via OpenAI embeddings + pgvector cosine similarity), not keywords.

## 📁 Repository structure

```
citadel-iq/
├── CitadelIQ.Domain/            # Entities, enums, domain rules — no external dependencies
├── CitadelIQ.Application/       # Use cases, interfaces, DTOs, AutoMapper profile
├── CitadelIQ.Infrastructure/    # EF Core + PostgreSQL/pgvector repos, local-disk storage, OpenAI SDK, text extraction
├── CitadelIQ.FluentMigrations/  # FluentMigrator schema migrations (owns the database schema)
├── CitadelIQ.Tests/             # xUnit unit + Testcontainers integration tests
├── CitadelIQ.Api/               # ASP.NET Core Web API — controllers, DI, config, middleware
│   └── App_Data/documents/      # Uploaded file bytes (gitignored, created at runtime)
├── citadel-iq-ui/               # React + TypeScript + Vite frontend
├── .claude/technical-designs/design.md  # Design document — decisions, rationale, architecture, roadmap
├── CLAUDE.md                    # Developer/AI-agent guide to the codebase
└── Requirements.pages           # Original requirements document
```

## 🚀 Quick start

### Prerequisites

- **.NET 10 SDK**
- **Node.js** 18+
- **Docker Desktop** (to run PostgreSQL with the pgvector extension locally)
- **OpenAI API key** (get one from [platform.openai.com](https://platform.openai.com))

### 1. Database setup (PostgreSQL + pgvector)

Run Postgres in Docker using the `pgvector/pgvector` image. Choose a password and use it in place of `<pw>`
(no angle brackets, and avoid `$`, spaces and quotes):

```bash
docker run -d --name citadeliq-pg \
  -e POSTGRES_PASSWORD=<pw> -e POSTGRES_DB=citadeliq \
  -p 5432:5432 \
  -v citadeliq-pgdata:/var/lib/postgresql/data \
  pgvector/pgvector:pg17
```

The `-v` named volume keeps your data when the container is stopped, restarted or recreated. If port 5432 is
already in use, map another one (e.g. `-p 5433:5432`) and use that port in the connection string.

Day-to-day container commands:

```bash
docker ps                       # check it's running
docker stop citadeliq-pg        # stop (data is kept)
docker start citadeliq-pg       # start again
```

**Two database logins.** Postgres superusers ignore row-level security, which keeps workspaces apart, so the API runs as
a restricted login and only the migrations use the `postgres` superuser. Create the restricted login once (choose a
second password `<app-pw>`):

```bash
docker exec -it citadeliq-pg psql -U postgres -c \
  "CREATE ROLE citadeliq_app LOGIN PASSWORD '<app-pw>' NOSUPERUSER NOBYPASSRLS;"
```

Set both connection strings (stored via user-secrets, never committed — `appsettings.json` has empty values):

```bash
cd CitadelIQ.Api
dotnet user-secrets set "ConnectionStrings:CitadelIQ"           "Host=localhost;Port=5432;Database=citadeliq;Username=citadeliq_app;Password=<app-pw>"
dotnet user-secrets set "ConnectionStrings:CitadelIQMigrations" "Host=localhost;Port=5432;Database=citadeliq;Username=postgres;Password=<pw>"
```

The schema (tables, pgvector column, indexes, row-level security policies, and the grants for `citadeliq_app`) is
created automatically by the FluentMigrator migrations when the API starts in Development. The API **refuses to start**
if `ConnectionStrings:CitadelIQ` uses a superuser or a role with `BYPASSRLS`.

> **Upgrading from before workspaces?** The initial migration was rewritten in place, so an existing database will not
> pick it up. Reset it (see *Resetting local data*) and clear `App_Data/documents`, then create `citadeliq_app` as above.

#### Querying the data with psql

`psql` ships inside the Postgres container, so nothing extra needs installing. Open an interactive session:

```bash
docker exec -it citadeliq-pg psql -U postgres -d citadeliq
```

The prompt changes to `citadeliq=#`. Type SQL and end every statement with `;`. If the prompt becomes
`citadeliq-#`, `psql` is still waiting for a `;` — finish the statement, or type `\r` to discard it.

Useful `psql` commands (no `;` needed):

```text
\dt                 list tables
\d "Documents"      show a table's columns, indexes and foreign keys
\x                  toggle expanded (one field per line) output — handy for wide rows
\q                  quit
```

Example queries:

```sql
SELECT * FROM "Workspaces";                 -- one row per individual / organization workspace
SELECT "DisplayName", "Email", "ClosedAtUtc" FROM "Users";
SELECT * FROM "Folders";                    -- each workspace has its own "Home" root (ParentFolderId IS NULL)
SELECT "FileName", "ProcessingStatus", "FailureReason" FROM "Documents";
SELECT "ChunkIndex", left("Text", 60) AS text, "ModelName", "Embedding" IS NOT NULL AS has_embedding
FROM "DocumentChunks";
SELECT * FROM "VersionInfo";                -- applied FluentMigrator migrations
```

Run a single query without opening a session:

```bash
docker exec -it citadeliq-pg psql -U postgres -d citadeliq -c 'SELECT "FileName", "ProcessingStatus" FROM "Documents";'
```

Notes:
- Connect as `postgres` (as above) to see every workspace's rows. As `citadeliq_app` you see nothing unless the session
  is stamped with a workspace: `SELECT set_config('app.workspace_id', '<workspace id>', false);`.
- Table and column names are case-sensitive, so keep the double quotes (`"Documents"`, not `Documents`).
- Avoid `SELECT *` on `"DocumentChunks"` — the embedding column prints 1536 numbers per row.
- If `psql` can't find the container, check it's running with `docker ps` (start it with `docker start citadeliq-pg`).
- Prefer a GUI? TablePlus, DBeaver or pgAdmin can connect to `localhost:5432` (database `citadeliq`, user `postgres`, your `<pw>`).

#### Resetting local data

Uploaded files and database rows are stored separately, so clear both.

**Wipe the database completely** (removes the container and its volume; the next `dotnet run` recreates the schema):

```bash
docker rm -f citadeliq-pg
docker volume rm citadeliq-pgdata
# then re-run the `docker run ...` command above, and create citadeliq_app again
```

**Keep the container, delete only the rows** (keeps the schema and `VersionInfo`; everyone onboards again):

```bash
docker exec -it citadeliq-pg psql -U postgres -d citadeliq
```
```sql
TRUNCATE "DocumentChunks", "Documents", "Folders", "JoinRequests", "Memberships", "Users", "Workspaces";
```

**Delete the uploaded files:**

```bash
rm -rf CitadelIQ.Api/App_Data/documents/*
```

To wipe everything, including Entra users, blob files and secrets, see [.claude/docs/cleanup.md](./.claude/docs/cleanup.md).

### 2. Backend setup

```bash
cd CitadelIQ.Api
dotnet restore

# Set your OpenAI credentials (never committed — stored outside the repo via user-secrets)
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
dotnet user-secrets set "OpenAI:EmbeddingModel" "text-embedding-3-small"

dotnet run
```

The API starts on `http://localhost:5157`.

**Sign-in.** In Development the API defaults to `Authentication:Mode = DevelopmentLogin`: a local form where any email
signs in (the same email is the same account), so you can work without an identity provider. It is refused outside
the Development environment. To sign in with **Microsoft Entra External ID** instead, follow
[Sign-in with Microsoft Entra External ID](#-sign-in-with-microsoft-entra-external-id) below.

The session is an HttpOnly cookie (`__Host-citadeliq`, Secure), encrypted with ASP.NET data-protection keys — locally in
`~/.aspnet/DataProtection-Keys`, in Docker on the `dpkeys` volume (`DataProtection:KeysPath`), so restarts don't sign
users out. Chrome and Firefox accept Secure cookies on
`http://localhost`; Safari does not — use Chrome/Firefox locally, or run the API with `--launch-profile https` and
`API_PROXY_TARGET=https://localhost:7274 npm run dev`.

### 3. Frontend setup

```bash
cd citadel-iq-ui
npm install
npm run dev
```

The UI opens at `http://localhost:5173`. The dev server proxies `/api`, `/auth` and the OIDC callback paths to the API
(`http://localhost:5157`, override with `API_PROXY_TARGET`), so the browser only ever talks to one origin.

### 4. Try it out

1. Open `http://localhost:5173` and sign in. On first sign-in choose **Just me** (a private workspace), **New
   organization** (you become its Owner), or **Join an organization** with a join code from its admin page — an Owner
   or Admin then approves you. You land on your workspace's empty **Home**.
2. Click **New folder** to create a folder (there's no seed data — you build the structure yourself).
   Each folder card has Open, Rename (inline text box, Enter to save/Escape to cancel), and Delete
   icons; deleting a folder cascades to every subfolder and document inside it. A **Back** button
   appears next to "New folder" whenever you're not at Home, for a quick way up a level.
3. Click **Upload** and drop in a PDF, DOCX, TXT, CSV, or XLSX file. Watch its status icon progress
   through *Extracting text… → Creating searchable sections… → Generating embeddings… → Ready*.
4. Click the search icon in the top bar, type a natural-language question, and see ranked results
   with similarity scores — even if your wording doesn't match the document's wording. Search
   defaults to the folder you're currently viewing; switch to "+ Subfolders" or "Entire workspace" to
   widen it.

## ⚙️ Configuration

| Section | Key | Default | Notes |
|---|---|---|---|
| `ConnectionStrings` | `CitadelIQ` | — (empty) | **User-secrets / env var only** — runtime login (`citadeliq_app`); refused if superuser/BYPASSRLS |
| `ConnectionStrings` | `CitadelIQMigrations` | — (empty) | **User-secrets / env var only** — owner login used to run migrations |
| `Authentication` | `Mode` | `Oidc` (`DevelopmentLogin` in Development) | The development login is refused outside Development |
| `Authentication:Oidc` | `Authority` / `ClientId` / `ClientSecret` | — | Entra External ID app; `ClientSecret` **user-secrets / env var only** |
| `DataProtection` | `KeysPath` | — (ASP.NET default) | Where session-cookie keys are kept; Docker uses the `dpkeys` volume |
| `OpenAI` | `EmbeddingModel` | `text-embedding-3-small` | Set via user-secrets |
| `OpenAI` | `EmbeddingDimension` | `1536` | Must match the model; the migration hardcodes `vector(1536)` |
| `OpenAI` | `ApiKey` | — | **User-secrets only**, never in `appsettings.json` |
| `Upload` | `MaxFileSizeMB` / `AllowedExtensions` | `20` / pdf, docx, txt, csv, xlsx | Configurable allow-list |
| `Chunking` | `ChunkSize` / `ChunkOverlap` | `400` / `80` | Characters |
| `Search` | `DefaultTopK` / `MaxTopK` | `10` / `50` | Result count |
| `Search` | `MinSimilarity` | `0.25` | Minimum cosine similarity for a result (0 disables); override via user-secrets |
| `Storage` | `DocumentsPath` | `App_Data/documents` | Raw file storage location |

See [CLAUDE.md](./CLAUDE.md) for the full configuration reference and architectural details.

## 🛠️ Technology stack

**Backend**
- ASP.NET Core Web API (.NET 10) — Clean Architecture (Domain / Application / Infrastructure / Api)
- PostgreSQL + pgvector — EF Core for queries, FluentMigrator for schema migrations
- Official OpenAI .NET SDK — `text-embedding-3-small`
- AutoMapper — entity → DTO mapping
- PdfPig, DocumentFormat.OpenXml, ClosedXML — text extraction (PDF, DOCX, XLSX)

**Frontend**
- React 19 + TypeScript + Vite
- Material UI (MUI) v9 — components & accessibility
- Tailwind CSS v4 — layout & spacing utilities
- React Router v7

## 📋 Available scripts

**Backend** (`CitadelIQ.Api/`)
```bash
dotnet run       # Run the API
dotnet build     # Build the solution
```

**Tests** (repo root — integration tests need Docker running; they start their own throwaway Postgres+pgvector)
```bash
dotnet test CitadelIQ.slnx                                       # all tests
dotnet test CitadelIQ.Tests --filter "FullyQualifiedName~Unit"   # unit tests only (no Docker)
dotnet test CitadelIQ.Tests --filter "FullyQualifiedName~Search" # tests with "Search" in the name
```

**Frontend** (`citadel-iq-ui/`)
```bash
npm run dev      # Start Vite dev server
npm run build    # Type-check + production build
npm run lint     # Run oxlint
npm run preview  # Preview the production build
```

## 📚 API endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| `GET` | `/api/me` | Session: onboarding / pending / active (workspace, role) / closed |
| `POST` | `/api/onboarding/individual`, `/organization`, `/join-requests` | First sign-in choices |
| `GET`/`PUT`/`DELETE` | `/api/organization/members…` | Members and roles (Admin/Owner) |
| `GET`/`POST` | `/api/organization/join-requests…`, `/join-code…` | Approvals and the join code (Admin/Owner) |
| `GET` | `/api/folders/root` | The caller's workspace Home folder id |
| `GET` | `/api/folders/{id}` | Folder metadata |
| `GET` | `/api/folders/{id}/contents` | Subfolders + documents + breadcrumb |
| `POST` | `/api/folders` | Create a folder |
| `PUT` | `/api/folders/{id}` | Rename a folder |
| `DELETE` | `/api/folders/{id}` | Delete a folder, cascading to all subfolders + their documents |
| `POST` | `/api/documents/upload` | Upload a document (multipart) |
| `GET` | `/api/documents/{id}/status` | Processing status (for polling) |
| `GET` | `/api/documents/{id}/download` | Download the original file |
| `DELETE` | `/api/documents/{id}` | Delete a document (removes stored file, chunks, and embeddings) |
| `POST` | `/api/search` | Semantic search |
| `GET` | `/health` | Health check |

## 🏗️ Architecture

```
User (Browser)
    ↓
React Frontend (localhost:5173)
    ↓ HTTP
ASP.NET Core API (localhost:5157)
    ↓                              ↓
PostgreSQL + pgvector       Local disk (App_Data/)
(folders, docs, chunks,     (raw uploaded file bytes)
 embeddings)
    ↓
OpenAI Embeddings API
```

Upload triggers an in-process, fire-and-forget processing pipeline (extract → chunk → embed →
store) so the upload request returns immediately; the UI polls for status until the document is
`Ready` or `Failed`. Search embeds the query once, then PostgreSQL + pgvector ranks the eligible chunks
by cosine distance in SQL and returns the top-K (see
[design.md](./.claude/technical-designs/design.md) and
[postgresql-pgvector.md](./.claude/technical-designs/postgresql-pgvector.md); authentication and RAG are
designed for, but intentionally not built in this version).

## 💬 Ask (answers from your documents)

The search drawer's **Ask** mode streams a short answer written only from your documents, with numbered citations
that link to the exact passages (each source card can download the original file). Follow-up questions work within a
session; the conversation is kept in the browser only. Configure via `OpenAI:ChatModel` and the `Rag` section
(limits, kill switch `Rag:Enabled`, per-user rate limits) — see `CLAUDE.md` for the table. **Privacy:** the question,
recent turns and the most relevant passages are sent to OpenAI; nothing is stored server-side and questions/answers
are never logged. Set a monthly spend limit in the OpenAI dashboard.

## 🔑 Sign-in with Microsoft Entra External ID

Locally the development login is enough. Use Entra to test the real sign-up/sign-in flow, and for every deployment.
Email, passwords, verification codes and password reset all live in Entra; CitadelIQ stores only the user's issuer,
subject id, display name and email. Set up the database first (Quick start step 1) and check the app with the
development login before switching.

> The Azure portal moves its menus around from time to time. Each step names what to look for, so you can find it
> under a different label if needed.

### 1. Create the external tenant

1. Go to [entra.microsoft.com](https://entra.microsoft.com) → **Entra ID → Overview → Manage tenants → Create** and choose
   **External** (not Workforce).
2. Fill in:
   - **Tenant name:** e.g. `CitadelIQ`.
   - **Domain name:** e.g. `citadeliq`. The portal shows the directory domain `citadeliq.onmicrosoft.com`; sign-in
     happens on `citadeliq.ciamlogin.com`. The prefix (`citadeliq`) is your `<subdomain>` below.
   - **Location:** your region.
   - **Subscription / resource group:** any. The resource group only holds the tenant's billing resource, not your
     users. Deleting that group can delete the tenant and all its users, so use a separate group (e.g.
     `rg-citadeliq-identity`) if you might tear down a shared one.
3. Create it (takes a few minutes), then switch to it: **Settings → Directories + subscriptions**.
4. On **Overview**, copy the **Tenant ID** (a GUID). This is `<tenant-id>`.

### 2. Register the app

In the external tenant:

1. **App registrations → New registration**:
   - **Name:** `CitadelIQ (local)`.
   - **Supported account types:** **Single tenant only – CitadelIQ** (accounts in this directory only). Everyone who
     signs up through the user flow becomes an account in this tenant. The multi-tenant and personal-account options
     would let outside accounts skip your sign-up flow.
   - **Redirect URI:** platform **Web**, `http://localhost:5173/signin-oidc`.
2. Copy the **Application (client) ID** from the app's **Overview**.
3. **Authentication:**
   - Add a second Web redirect URI, `http://localhost:5173/signout-callback-oidc`, for returning after sign-out.
   - Leave both **Implicit grant and hybrid flows** boxes unchecked. The app uses the authorization-code flow with PKCE.
4. **Certificates & secrets → New client secret.** Copy the **Value** (not the Secret ID); it's shown only once. Note
   the expiry date: sign-in stops working when the secret expires.
5. **API permissions:** Microsoft Graph *delegated* `openid`, `profile` and `email` should be listed (add any that are
   missing), then click **Grant admin consent**.
6. **Token configuration → Add optional claim → ID → email**. Accept if it asks to turn on the Graph email permission.

### 3. Create the sign-up / sign-in user flow

1. **External Identities → User flows → New user flow**:
   - **Name:** `SignUpSignIn`.
   - **Identity providers:** **Email with password**. New users confirm their email with a one-time code.
   - **User attributes:** tick **Display Name**. The app shows it and uses it to name individual workspaces.
2. Open the flow → **Applications → Add application** → select `CitadelIQ (local)`. **Without this link the sign-in
   page has no "No account? Create one" option.**
3. Optional: **Company branding** (logo, colours). Self-service password reset is on by default for email + password.

### 4. Check the sign-in address

Open this in a browser:

```
https://<subdomain>.ciamlogin.com/<tenant-id>/v2.0/.well-known/openid-configuration
```

If it returns JSON with an `issuer`, the same URL without `/.well-known/openid-configuration` is your **Authority**.
If it doesn't, copy the OpenID Connect metadata URL from the app registration's **Overview → Endpoints** and drop that
same suffix. Don't use `login.microsoftonline.com`: that host is for workforce tenants and won't run your sign-up flow.

### 5. Point the app at Entra

```bash
cd CitadelIQ.Api
dotnet user-secrets set "Authentication:Mode" "Oidc"
dotnet user-secrets set "Authentication:Oidc:Authority" "https://<subdomain>.ciamlogin.com/<tenant-id>/v2.0"
dotnet user-secrets set "Authentication:Oidc:ClientId" "<application (client) id>"
dotnet user-secrets set "Authentication:Oidc:ClientSecret" "<client secret value>"
```

User-secrets override `appsettings.Development.json`, which defaults to `DevelopmentLogin`. To switch back to the
development form: `dotnet user-secrets set "Authentication:Mode" "DevelopmentLogin"`.

Restart `dotnet run`, then in Chrome or Firefox:

1. Open `http://localhost:5173` → **Sign in or create an account**. You should be sent to `…ciamlogin.com`.
2. Choose **No account? Create one**, then enter your email, the code you receive, a password and a display name.
3. You return to CitadelIQ's onboarding page, which should say *Welcome, &lt;your display name&gt;*.
4. **Sign out** from the account menu goes through Entra's sign-out and back to the sign-in page.

**Development-login accounts don't carry over.** They're stored with the issuer `urn:citadeliq:dev`, so signing in
with the same email through Entra creates a new user who starts at onboarding; nothing is merged. The old users,
organizations and documents stay in the database but can't sign in until you switch back to `DevelopmentLogin`. To
start clean, wipe the rows and files (see *Resetting local data*) and sign out in the browser.

### Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| **AADSTS50011** (redirect URI mismatch) | The URI differs from the registered one | Use exactly `http://localhost:5173/signin-oidc`: `localhost`, not `127.0.0.1`, and port 5173, not 5157 |
| Sign-out error about the post-logout redirect | `signout-callback-oidc` isn't registered | Step 2.3 |
| No **No account? Create one** link | The user flow isn't linked to this app, the client id belongs to another app, or the flow has no sign-up method | Step 3.2; compare `ClientId` with the app's Overview; check the address bar is `<subdomain>.ciamlogin.com`. Preview the flow with **Run user flow**: if the preview also lacks the link, tick **Email with password** under the flow's **Identity providers** |
| Display name shows the email | No `name` claim | Collect **Display Name** in the user flow, then sign out and in (the name is refreshed at every sign-in) |
| Email empty in the admin member list | No `email` claim | Step 2.6 |
| Back on the sign-in page, or "Correlation failed" | The browser dropped the Secure cookie | Use Chrome/Firefox on `http://localhost:5173`. For Safari run the API with `--launch-profile https` and the UI with `API_PROXY_TARGET=https://localhost:7274 npm run dev` |
| Lands on `/?signin=failed` | Entra returned an error | Check the API log; usually a wrong client secret or Authority |
| API won't start: "Authentication:Oidc:… is required" | A setting is missing | `dotnet user-secrets list` and set it |

### Deploying with Docker Compose

See **[.claude/docs/docker-deployment.md](./.claude/docs/docker-deployment.md)**. It covers HTTPS and forwarded headers,
production redirect URIs and secret, `.env`, the `citadeliq_app` role on new and existing volumes, volumes and
backups, network settings and troubleshooting.

## ☁️ Optional: Azure Blob Storage for files

By default uploads are stored on local disk. To store them in Azure Blob Storage instead:

**1. Create the resources once** (Azure CLI; storage account names must be 3–24 lowercase letters/digits and globally unique):

```bash
az login

RG=rg-citadeliq
LOCATION=eastus
ACCOUNT=citadeliqdocs01      # change this
CONTAINER=documents

az group create --name $RG --location $LOCATION

az storage account create --name $ACCOUNT --resource-group $RG --location $LOCATION \
  --sku Standard_LRS --kind StorageV2 --allow-blob-public-access false --min-tls-version TLS1_2

# Private container (no anonymous access)
az storage container create --name $CONTAINER --account-name $ACCOUNT --auth-mode login --public-access off
```

If the container command is denied, grant yourself data access first (also needed to use `az login` auth from the app):

```bash
az role assignment create --role "Storage Blob Data Contributor" \
  --assignee "$(az ad signed-in-user show --query id -o tsv)" \
  --scope "$(az storage account show --name $ACCOUNT --resource-group $RG --query id -o tsv)"
```

**2. Configure the API** (run from `CitadelIQ.Api/`). Pick **one** authentication option:

```bash
cd CitadelIQ.Api
dotnet user-secrets set "Storage:Provider" "AzureBlob"
dotnet user-secrets set "Storage:AzureBlob:AccountName" "$ACCOUNT"
dotnet user-secrets set "Storage:AzureBlob:ContainerName" "$CONTAINER"

# Option A: connection string (simplest for local dev; takes precedence if set)
dotnet user-secrets set "Storage:AzureBlob:ConnectionString" "$(az storage account show-connection-string --name $ACCOUNT --resource-group $RG --query connectionString -o tsv)"

# Option B: no secret — skip the line above and rely on `az login` (needs the role assignment above);
# in Azure, use a managed identity with the same role.
```

For Docker Compose set `STORAGE_PROVIDER=AzureBlob`, `AZURE_STORAGE_ACCOUNT`, `AZURE_STORAGE_CONTAINER` and (option A)
`AZURE_STORAGE_CONNECTION_STRING` in `.env`.

**3. Verify**

```bash
dotnet run                                   # in CitadelIQ.Api/
curl http://localhost:5157/health/storage    # {"status":"healthy","provider":"AzureBlob"}
```

Upload a file in the UI, then confirm the blob exists:

```bash
az storage blob list --account-name $ACCOUNT --container-name $CONTAINER --auth-mode login --output table
```

To go back to local disk: `dotnet user-secrets set "Storage:Provider" "LocalDisk"`. Files already on local disk are not
migrated to the container (and vice versa).

## 🔐 Security notes

- OpenAI API key lives in `dotnet user-secrets`, never in source or sent to the frontend.
- All API errors are routed through a global exception handler that returns safe, generic messages
  — no stack traces, exception types, or internal configuration are ever exposed.
- File uploads are validated on the backend (type + size) regardless of frontend validation.
- Sign-in is delegated to an OIDC provider (Entra External ID); the browser holds only an HttpOnly session cookie, and
  every state-changing request needs the `X-CSRF` header.
- Every folder, document, chunk and stored file belongs to one workspace. Isolation is enforced twice: by the
  application (EF Core query filters) and by PostgreSQL row-level security, with the API connected as a role that
  cannot bypass it. Another workspace's ids answer 404.
- Only Admins and Owners delete or rename; an organization always keeps at least one Owner.

## 📖 Further reading

- **[design.md](./.claude/technical-designs/design.md)** — the design document: decisions made, rationale, and the full roadmap
- **[CLAUDE.md](./CLAUDE.md)** — architecture deep-dive and development guide
