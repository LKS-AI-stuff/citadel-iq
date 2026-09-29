# CitadelIQ

An AI-powered document management and semantic search portal — built to demonstrate the
embeddings → chunking → cosine-similarity pipeline behind semantic search, wrapped in a
professional, enterprise-style UI. This is deliberately **not** a chatbot: there's no AI-generated
answer yet, just ranked, relevant document chunks returned for a natural-language query.

## 🎯 What it does

Upload documents, organize them into folders, and search their contents using natural language —
even when your search terms don't literally appear in the text. For example, a document containing:

> "Employees are eligible for 15 days of annual vacation."

is found by searching:

> "How many vacation days can an employee take?"

because the search matches on **meaning** (via OpenAI embeddings + cosine similarity), not keywords.

## 📁 Repository structure

```
citadel-iq/
├── CitadelIQ.Domain/            # Entities, enums, domain rules — no external dependencies
├── CitadelIQ.Application/       # Use cases, interfaces, DTOs, AutoMapper profile
├── CitadelIQ.Infrastructure/    # In-memory repos, local-disk storage, OpenAI SDK, text extraction
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
- **OpenAI API key** (get one from [platform.openai.com](https://platform.openai.com))

### 1. Backend setup

```bash
cd CitadelIQ.Api
dotnet restore

# Set your OpenAI credentials (never committed — stored outside the repo via user-secrets)
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
dotnet user-secrets set "OpenAI:EmbeddingModel" "text-embedding-3-small"

dotnet run
```

The API starts on `http://localhost:5157`.

### 2. Frontend setup

```bash
cd citadel-iq-ui
npm install
cp .env.example .env.local   # if .env.local doesn't already exist
npm run dev
```

The UI opens at `http://localhost:5173`.

### 3. Try it out

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
| `OpenAI` | `EmbeddingModel` | `text-embedding-3-small` | In `appsettings.json` |
| `OpenAI` | `ApiKey` | — | **User-secrets only**, never in `appsettings.json` |
| `Upload` | `MaxFileSizeMB` / `AllowedExtensions` | `20` / pdf, docx, txt, csv, xlsx | Configurable allow-list |
| `Chunking` | `ChunkSize` / `ChunkOverlap` | `400` / `80` | Characters |
| `Search` | `DefaultTopK` / `MaxTopK` | `10` / `50` | Result count |
| `Storage` | `DocumentsPath` | `App_Data/documents` | Raw file storage location |

See [CLAUDE.md](./CLAUDE.md) for the full configuration reference and architectural details.

## 🛠️ Technology stack

**Backend**
- ASP.NET Core Web API (.NET 10) — Clean Architecture (Domain / Application / Infrastructure / Api)
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
In-memory stores            Local disk (App_Data/)
(folders, docs, chunks,     (raw uploaded file bytes)
 embeddings — lost on
 restart)
    ↓
OpenAI Embeddings API
```

Upload triggers an in-process, fire-and-forget processing pipeline (extract → chunk → embed →
store) so the upload request returns immediately; the UI polls for status until the document is
`Ready` or `Failed`. Search embeds the query once, then computes cosine similarity against every
eligible chunk's stored embedding entirely in-app — no vector database yet (see
[design.md](./.claude/technical-designs/design.md) for the full roadmap: PostgreSQL + pgvector,
authentication, and RAG are all designed for, but intentionally not built in this version).

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
