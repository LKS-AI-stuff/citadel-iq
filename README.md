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

Set the connection string (stored via user-secrets, never committed — `appsettings.json` has an empty value):

```bash
cd CitadelIQ.Api
dotnet user-secrets set "ConnectionStrings:CitadelIQ" "Host=localhost;Port=5432;Database=citadeliq;Username=postgres;Password=<pw>"
```

The schema (tables, pgvector column, indexes, root "Home" folder) is created automatically by the
FluentMigrator migrations when the API starts in Development.

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
SELECT * FROM "Folders";                    -- includes the seeded "Home" root folder
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
# then re-run the `docker run ...` command above
```

**Keep the container, delete only the rows** (keeps the schema and the root "Home" folder — don't truncate
`Folders` or `VersionInfo`):

```bash
docker exec -it citadeliq-pg psql -U postgres -d citadeliq
```
```sql
TRUNCATE "DocumentChunks", "Documents";
DELETE FROM "Folders" WHERE "Id" <> '00000000-0000-0000-0000-000000000000';
```

**Delete the uploaded files:**

```bash
rm -rf CitadelIQ.Api/App_Data/documents/*
```

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

### 3. Frontend setup

```bash
cd citadel-iq-ui
npm install
cp .env.example .env.local   # if .env.local doesn't already exist
npm run dev
```

The UI opens at `http://localhost:5173`.

### 4. Try it out

1. Open `http://localhost:5173` — you'll land on an empty **Home**.
2. Click **New folder** to create a folder (there's no seed data — you build the structure yourself).
   Each folder card has Open, Rename (inline text box, Enter to save/Escape to cancel), and Delete
   icons; deleting a folder cascades to every subfolder and document inside it. A **Back** button
   appears next to "New folder" whenever you're not at Home, for a quick way up a level.
3. Click **Upload** and drop in a PDF, DOCX, TXT, CSV, or XLSX file. Watch its status icon progress
   through *Extracting text… → Creating searchable sections… → Generating embeddings… → Ready*.
4. Click the search icon in the top bar, type a natural-language question, and see ranked results
   with similarity scores — even if your wording doesn't match the document's wording. Search
   defaults to the folder you're currently viewing; switch to "+ Subfolders" or "Entire portal" to
   widen it.

## ⚙️ Configuration

| Section | Key | Default | Notes |
|---|---|---|---|
| `ConnectionStrings` | `CitadelIQ` | — (empty) | **User-secrets / env var only** — PostgreSQL connection string |
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
| `GET` | `/api/folders/root` | Well-known Home folder id |
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

## 🔐 Security notes

- OpenAI API key lives in `dotnet user-secrets`, never in source or sent to the frontend.
- All API errors are routed through a global exception handler that returns safe, generic messages
  — no stack traces, exception types, or internal configuration are ever exposed.
- File uploads are validated on the backend (type + size) regardless of frontend validation.
- Authentication/authorization are explicitly out of scope for this version, but the architecture
  (interfaces around every repository and external service) is shaped so they can be added later
  without reworking the Application layer.

## 📖 Further reading

- **[design.md](./.claude/technical-designs/design.md)** — the design document: decisions made, rationale, and the full roadmap
- **[CLAUDE.md](./CLAUDE.md)** — architecture deep-dive and development guide
