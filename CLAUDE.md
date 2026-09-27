# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**CitadelIQ** is an AI-powered document management and semantic search portal — a professional,
enterprise-style application, not a chatbot. Users organize documents into folders, upload files,
and run natural-language searches that return the most relevant document chunks ranked by cosine
similarity against OpenAI embeddings. There is no AI-generated answer yet; this version stops at
"vector search → ranked results" by design (see [PLAN.md](./PLAN.md) §1, §6, §11, §20 for the
full rationale and the documented future RAG phase).

It's a monorepo with two components:

- **Backend**: ASP.NET Core Web API (.NET 10), Clean Architecture, C#
- **Frontend**: React 19 + TypeScript + Vite, Material UI + Tailwind CSS

The authoritative design document is [PLAN.md](./PLAN.md) — read it before making architectural
changes. It records the decisions made during planning (in-memory storage now, EF Core/Postgres
deferred; disk-based raw file storage; fire-and-forget processing instead of a job queue) along
with the reasoning, so a change that looks like an obvious improvement may already be a
deliberately deferred one.

## Architecture

### High-level design

```
React UI (Vite) ←→ ASP.NET Core API ←→ OpenAI Embeddings API
                          │
                          ├── In-memory metadata/chunk/embedding stores (lost on restart)
                          └── Local disk (App_Data/documents) for raw uploaded file bytes
```

### Backend — Clean Architecture

```
CitadelIQ.sln
 ├─ CitadelIQ.Domain          (no dependencies)
 ├─ CitadelIQ.Application     (depends on Domain)
 ├─ CitadelIQ.Infrastructure  (depends on Application, Domain)
 └─ CitadelIQ.Api             (depends on Application, Infrastructure)
```

**Domain** (`CitadelIQ.Domain/`): `Folder`, `Document`, `DocumentChunk`, `DocumentEmbedding`
entities; `ProcessingStatus` and `SearchScope` enums; `DomainException`. `Folder.RootId` is a
well-known `Guid.Empty` — the single "Home" folder always has this id, so the frontend never needs
to look it up.

**Application** (`CitadelIQ.Application/`): use cases, interfaces, DTOs — no external
dependencies (no OpenAI SDK, no ASP.NET, no disk I/O). Key pieces:
- `Folders/FolderService`, `Folders/FolderPathBuilder` (shared ancestor-walk logic for
  breadcrumbs and search result folder-path display)
- `Documents/DocumentService` (upload → validate → save → dispatch background processing),
  `Documents/TextChunker` (configurable chunk size/overlap)
- `Search/SearchService`, `Search/CosineSimilarity`
- `Interfaces/` — repository and service abstractions (`IFolderRepository`,
  `IDocumentRepository`, `IDocumentChunkRepository`, `IEmbeddingRepository`, `IDocumentStorage`,
  `IOpenAIEmbeddingService`, `ITextExtractor`, `ITextExtractionService`, `IFileValidator`,
  `IDocumentProcessingDispatcher`) — every external or storage dependency is behind one of these
- `Options/` — `UploadOptions`, `ChunkingOptions`, `SearchOptions`, `OpenAIOptions`,
  `StorageOptions`, all bound from configuration, never hardcoded
- `Mapping/MappingProfile` — AutoMapper profile for entity → DTO conversion (no manual `ToDto`
  methods)

**Infrastructure** (`CitadelIQ.Infrastructure/`): the only layer allowed to depend on OpenAI, the
filesystem, or a specific persistence mechanism.
- `Persistence/InMemory*Repository` — `ConcurrentDictionary`-backed, singleton, global (not
  per-session — see PLAN.md §3 for why). Swapping these for Postgres/EF Core later is an
  Infrastructure-only change.
- `Storage/LocalDiskDocumentStorage` — raw file bytes go to `App_Data/documents/` (configurable
  via `Storage:DocumentsPath`), **not** `bin/` (which `dotnet build`/`clean` wipes) and **not**
  RAM (see PLAN.md §3 for the reasoning: keeps memory pressure off large uploads).
- `TextExtraction/` — `PdfTextExtractor` (PdfPig), `DocxTextExtractor` (DocumentFormat.OpenXml),
  `XlsxTextExtractor` (ClosedXML), `PlainTextExtractor` (.txt/.csv), resolved by
  `TextExtractionService` via `IEnumerable<ITextExtractor>` + `CanHandle(extension)`.
- `AI/OpenAIEmbeddingService` — wraps the official OpenAI .NET SDK (`OpenAI.Embeddings.EmbeddingClient`).
  Lazily constructs the SDK client so a missing API key doesn't crash startup — it only throws
  (with a message caught and turned into a safe user-facing error) when an embedding is actually
  requested.
- `Processing/BackgroundDocumentProcessingDispatcher` — see "Upload & processing pipeline" below.
- `Validation/FileValidator` — extension/size checks from `UploadOptions`.

**Api** (`CitadelIQ.Api/`): thin controllers, DI registration, CORS, global exception middleware,
config binding. `Middleware/ExceptionHandlingMiddleware` converts `DomainException` /
`Application.Common.ValidationException` / `Application.Common.NotFoundException` into safe
`ProblemDetails` responses and maps everything else to a generic 500 — never leaking stack traces,
exception types, or configuration.

### Upload & processing pipeline

Upload is a two-phase, fire-and-forget flow, **not** a real background job queue (that's
explicitly out of scope for this version — see PLAN.md §9):

1. `POST /api/documents/upload` validates the file, saves raw bytes to disk, records the
   `Document` (status `Uploaded`), and calls `IDocumentProcessingDispatcher.Dispatch(documentId)`
   — this schedules `Task.Run` on a **new DI scope** (the HTTP request's scope is disposed once
   the response is sent) and returns immediately.
2. The dispatched task runs `DocumentService.ProcessDocumentAsync`: extract text → chunk → call
   OpenAI for embeddings (batched, one call per chunk batch — **never one embedding for the whole
   document**) → store chunks + embeddings → mark `Ready`, or `MarkFailed` with a safe message on
   any exception (unexpected exceptions get a generic message; expected ones —
   `DocumentProcessingException` for empty/unreadable documents — surface their own message).
3. The frontend polls `GET /api/documents/{id}/status` (see `FolderPage.tsx`'s 2-second interval
   effect) to progress the UI through `ExtractingText → Chunking → GeneratingEmbeddings →
   Ready/Failed`, and fires a toast when a document transitions from in-progress to settled.

Because everything (metadata, chunks, embeddings) is in-memory, **restarting the API loses all of
it** — but the raw files on disk survive, so after a restart you'll see an empty folder tree with
orphaned files still in `App_Data/documents` until a future cleanup step exists.

### Search flow

`POST /api/search` → `SearchService.SearchAsync`:
1. Validates the query is non-empty.
2. Resolves eligible documents by `SearchScope` (`EntirePortal` / `CurrentFolder` /
   `CurrentFolderAndSubfolders`) — scope resolution and folder-tree walking happen **server-side**;
   the frontend only sends `currentFolderId` + `searchScope`.
3. Filters to documents with `ProcessingStatus.Ready` (in-progress/failed documents are silently
   excluded from results, not treated as a hard error).
4. Embeds the query once, computes cosine similarity (`CosineSimilarity.Compute`) against every
   eligible chunk's stored embedding, sorts descending, takes top-K (`SearchOptions.DefaultTopK`,
   overridable per-request up to `SearchOptions.MaxTopK`).
5. Builds each result's folder-path display via `FolderPathBuilder`, omitting the root "Home"
   segment (e.g. `"HR / Policies"`, not `"Home / HR / Policies"`), matching the requirements'
   example format.

Similarity/ranking/top-K are all implemented in-app — the OpenAI SDK is used **only** to generate
embeddings, per the requirements' explicit constraint (no vector DB, no Semantic Kernel, no
LangChain, no RAG answer generation yet).

### Frontend architecture

```
citadel-iq-ui/src/
 ├─ app/            AppShell (layout + search panel host), TopNavigation, FolderPage (route)
 ├─ components/
 │   ├─ folders/    Breadcrumbs, FolderCard, CreateFolderDialog
 │   ├─ documents/  FileCard, FileIcon, ProcessingStatusChip
 │   ├─ upload/     UploadDialog, UploadDropzone
 │   ├─ search/     SearchPanel, SearchScopeSelector, SearchInput, SearchResults, SearchResultCard
 │   └─ common/      EmptyState, LoadingState, ToastProvider, CardActionsMenu
 ├─ hooks/          useFolderContents, useCreateFolder, useUpload, useSearch
 ├─ api/            apiClient (fetch wrapper + ApiError), foldersApi, documentsApi, searchApi
 ├─ theme/          ColorModeProvider (light/dark, persisted to localStorage), theme.ts
 ├─ types/          folder.ts, search.ts — hand-kept in sync with backend DTOs
 └─ utils/          highlightMatches.tsx — best-effort literal term highlighting in search snippets
```

Notes:
- `AppShell` derives the "current folder" for the search panel via `useParams()` — React Router
  v6 merges params from the whole matched route branch, so this works even though `AppShell` is
  the parent layout route and `folderId` is defined on the child route.
- MUI resolved to **v9.4.0** — its `Box`/`Stack`/`Typography` do **not** accept layout props
  (`display`, `gap`, `fontWeight`, etc.) directly; everything must go through the `sx` prop. All
  components in this codebase already follow this; if you copy an example from older MUI docs
  that uses `<Box display="flex">`, it will fail to typecheck here.
- Tailwind's `preflight` is disabled (`tailwind.config.js`) to avoid fighting MUI's `CssBaseline`.
- Uploads use raw `XMLHttpRequest` (not `fetch`) specifically for `xhr.upload.onprogress` — `fetch`
  has no upload-progress event.
- `useUpload`'s callback is passed a completed upload's `DocumentSummaryDto`; `FolderPage` uses it
  to `refetch()` the folder contents, which also kicks off the settle-polling effect.

## Development

### Backend

```bash
cd CitadelIQ.Api
dotnet restore
dotnet build
dotnet run
```

Runs on `http://localhost:5157` by default (see `Properties/launchSettings.json`).

**Required secrets** (never in `appsettings.json` — that file is committed to git):
```bash
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
dotnet user-secrets set "OpenAI:EmbeddingModel" "text-embedding-3-small"
```

### Frontend

```bash
cd citadel-iq-ui
npm install
npm run dev      # http://localhost:5173
npm run build    # tsc -b && vite build
npm run lint      # oxlint
```

`.env.local` (gitignored) sets `VITE_API_URL` — copy from `.env.example` if it doesn't exist.

### Running both

Start the backend first (`dotnet run` in `CitadelIQ.Api/`), then the frontend (`npm run dev` in
`citadel-iq-ui/`). CORS is configured for `http://localhost:5173` via `Cors:AllowedOrigins` in
`appsettings.json`.

## Configuration reference

All in `CitadelIQ.Api/appsettings.json`, bound to `Options` classes in `CitadelIQ.Application`:

| Section | Key | Default | Purpose |
|---|---|---|---|
| `Cors` | `AllowedOrigins` | `["http://localhost:5173"]` | Allowed frontend origins |
| `OpenAI` | `EmbeddingModel` | `text-embedding-3-small` | Embedding model name |
| `OpenAI` | `ApiKey` | *(user-secrets only)* | Never in `appsettings.json` |
| `Upload` | `MaxFileSizeMB` | `20` | Rejected before processing starts |
| `Upload` | `AllowedExtensions` | `.pdf .docx .txt .csv .xlsx` | Configurable allow-list, not hardcoded |
| `Chunking` | `ChunkSize` / `ChunkOverlap` | `400` / `80` (tuned down from `800`/`150` — smaller chunks discriminate better between topically-similar documents) | Characters per chunk / overlap |
| `Search` | `DefaultTopK` / `MaxTopK` | `10` / `50` | Result count defaults/caps |
| `Storage` | `DocumentsPath` | `App_Data/documents` | Relative to `IHostEnvironment.ContentRootPath` — resolved once at host startup, not `Directory.GetCurrentDirectory()` (which depends on how the process was launched) |

## API contract

```
GET    /api/folders/root                    # well-known Home folder id (Guid.Empty)
GET    /api/folders/{folderId}              # folder metadata
GET    /api/folders/{folderId}/contents     # folder + breadcrumb (folderPath) + subfolders + documents
POST   /api/folders                         # { parentFolderId, name }
POST   /api/documents/upload                # multipart: folderId, file
GET    /api/documents/{documentId}/status   # { id, status, failureReason }
GET    /api/documents/{documentId}/download # streams original file, original filename
POST   /api/search                          # { query, currentFolderId, searchScope, topK? }
GET    /health
```

`ProcessingStatus` and `SearchScope` enums serialize as PascalCase strings (JSON string enum
converter registered in `Program.cs`), e.g. `"searchScope": "CurrentFolderAndSubfolders"`.

## Important implementation principles (carry these into any change)

1. Keep the backend Clean Architecture — Application never references Infrastructure or Api;
   Domain never references anything.
2. Keep business logic out of controllers — they only map HTTP ↔ Application calls.
3. Every external dependency (OpenAI, disk, in-memory store) sits behind an Application-layer
   interface — this is what makes the future Postgres/pgvector swap an Infrastructure-only change.
4. File-size/type limits and chunk size/overlap are configuration, not hardcoded — see the table
   above.
5. Never expose API keys, stack traces, or internal exception details in an API response — route
   all failures through `ExceptionHandlingMiddleware`'s known exception types, or accept the
   generic 500 message.
6. Async everywhere for I/O — file processing and OpenAI calls.
7. No video uploads, no vector database yet, no Semantic Kernel, no LangChain, no RAG
   answer-generation — these are documented future phases, not oversights.

## Out of scope for this version

Authentication/authorization, per-user document spaces, PostgreSQL/pgvector, EF Core/FluentMigrator,
RAG/LLM-generated answers, search history/pagination, hybrid keyword search, folder/file rename,
move, delete, bulk operations, document previews, audit logging, background job queues (the
current dispatcher is in-process fire-and-forget, not a persistent queue). See
[PLAN.md](./PLAN.md) §9 and §21 for the full future-enhancements list and the reasoning behind
each deferral.
