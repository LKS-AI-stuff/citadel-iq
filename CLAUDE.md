# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**CitadelIQ** is an AI-powered document management and semantic search portal — a professional,
enterprise-style application, not a chatbot. Users organize documents into folders, upload files,
and run natural-language searches that return the most relevant document chunks ranked by cosine
similarity against OpenAI embeddings. Search returns ranked passages; an **Ask** mode on top streams a short, cited, AI-written answer grounded only in
the retrieved passages (RAG — see [rag.md](./.claude/technical-designs/rag.md); the original deliberate stop at
"vector search → ranked results" is in [design.md](./.claude/technical-designs/design.md) §1, §6, §11, §20).

It's a monorepo with two components:

- **Backend**: ASP.NET Core Web API (.NET 10), Clean Architecture, C#
- **Frontend**: React 19 + TypeScript + Vite, Material UI + Tailwind CSS

The authoritative design document is [design.md](./.claude/technical-designs/design.md) — read it before making architectural
changes. It records the decisions made during planning (originally in-memory storage — since replaced by
PostgreSQL + pgvector, see [postgresql-pgvector.md](./.claude/technical-designs/postgresql-pgvector.md);
disk-based raw file storage; fire-and-forget processing instead of a job queue) along with the
reasoning, so a change that looks like an obvious improvement may already be a deliberately deferred one.

## Architecture

### High-level design

```
React UI (Vite) ←→ ASP.NET Core API ←→ OpenAI Embeddings API
                          │
                          ├── PostgreSQL + pgvector (folders, documents, chunks, embeddings)
                          └── Raw uploaded file bytes: local disk (App_Data/documents) or Azure Blob Storage (Storage:Provider)
```

### Backend — Clean Architecture

```
CitadelIQ.sln
 ├─ CitadelIQ.Domain          (no dependencies)
 ├─ CitadelIQ.Application     (depends on Domain)
 ├─ CitadelIQ.Infrastructure  (depends on Application, Domain)
 ├─ CitadelIQ.FluentMigrations (schema migrations; no project dependencies)
 └─ CitadelIQ.Api             (depends on Application, Infrastructure, FluentMigrations)
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
- `Rag/` (`AnswerService`, `AnswerRun`, `AnswerStreamProcessor`, `PromptBuilder`, `QuestionRewriter`, `ContextSelector`,
  `CitationParser`, `AskRequestValidator`), `Settings/AppSettingsService`, `Interfaces/IChatCompletionService`
- `Search/SearchService` (resolves scope → folder ids, embeds the query, calls `IVectorSearchRepository`).
  `Search/CosineSimilarity` is **obsolete** (superseded by pgvector) and unused — kept for reference.
- `Interfaces/` — repository and service abstractions (`IFolderRepository`,
  `IDocumentRepository`, `IDocumentChunkRepository`, `IEmbeddingRepository` (attaches vectors to
  persisted chunks), `IVectorSearchRepository` (similarity query), `IDocumentStorage`,
  `IOpenAIEmbeddingService`, `ITextExtractor`, `ITextExtractionService`, `IFileValidator`,
  `IDocumentProcessingDispatcher`) — every external or storage dependency is behind one of these
- `Options/` — `UploadOptions`, `ChunkingOptions`, `SearchOptions`, `OpenAIOptions`,
  `StorageOptions`, all bound from configuration, never hardcoded
- `Mapping/MappingProfile` — AutoMapper profile for entity → DTO conversion (no manual `ToDto`
  methods)

**Infrastructure** (`CitadelIQ.Infrastructure/`): the only layer allowed to depend on OpenAI, the
filesystem, or a specific persistence mechanism.
- `Persistence/` — EF Core + PostgreSQL + pgvector (`CitadelIQDbContext`, `Configurations/`,
  scoped `*Repository` classes; EF is used for queries only). The embedding is a pgvector `vector(N)` column on
  `DocumentChunks` (shadow properties `Embedding`/`ModelName`, not on the domain entity), with an HNSW
  cosine index. **The schema is owned by `CitadelIQ.FluentMigrations`** (FluentMigrator; the EF model just mirrors it). `VectorSearchRepository` runs the similarity query (`ORDER BY embedding <=> @q LIMIT k`)
  after filtering to `Ready` documents and the folder-id set `SearchService` resolved. The unique
  `(ParentFolderId, lower(Name))` index lives in the migration; `FolderRepository` translates its
  violation into the usual "already exists" `ValidationException`. DbContext is scoped, so each background
  processing task (own DI scope) gets its own.
- `Storage/AzureBlobDocumentStorage` — alternative `IDocumentStorage` (`Storage:Provider=AzureBlob`): blobs named
  `{documentId}{extension}` in a private container; auth is the connection string if set, otherwise
  `DefaultAzureCredential` (managed identity / `az login`) against `https://{AccountName}.blob.core.windows.net`.
  Account and container names are config. Lazy client, seekable read stream, idempotent delete, 404 → `FileNotFoundException`.
  Also implements `IStorageProbe` (used by `GET /health/storage`). Design: [azure-blob-storage.md](./.claude/technical-designs/azure-blob-storage.md).
- `Storage/LocalDiskDocumentStorage` — (default provider) raw file bytes go to `App_Data/documents/` (configurable
  via `Storage:DocumentsPath`), **not** `bin/` (which `dotnet build`/`clean` wipes) and **not**
  RAM (see .claude/technical-designs/design.md §3 for the reasoning: keeps memory pressure off large uploads).
- `TextExtraction/` — `PdfTextExtractor` (PdfPig), `DocxTextExtractor` (DocumentFormat.OpenXml),
  `XlsxTextExtractor` (ClosedXML), `PlainTextExtractor` (.txt/.csv), resolved by
  `TextExtractionService` via `IEnumerable<ITextExtractor>` + `CanHandle(extension)`.
- `AI/OpenAIChatCompletionService` — wraps `ChatClient` (lazy, like the embedding service); maps SDK failures to `AnswerGenerationException`.
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
explicitly out of scope for this version — see .claude/technical-designs/design.md §9):

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

Metadata, chunks and embeddings persist in PostgreSQL, so they survive restarts. A document still being
processed when the API stops stays in its in-progress status (the dispatcher is not a persistent queue).

### Search flow

`POST /api/search` → `SearchService.SearchAsync`:
1. Validates the query is non-empty.
2. Resolves eligible documents by `SearchScope` (`EntirePortal` / `CurrentFolder` /
   `CurrentFolderAndSubfolders`) — scope resolution and folder-tree walking happen **server-side**;
   the frontend only sends `currentFolderId` + `searchScope`.
3. Filters to documents with `ProcessingStatus.Ready` (in-progress/failed documents are silently
   excluded from results, not treated as a hard error).
4. Embeds the query once and hands it, with the resolved folder-id set (`null` = entire portal) and top-K
   (`SearchOptions.DefaultTopK`, overridable per-request up to `SearchOptions.MaxTopK`), to
   `IVectorSearchRepository`, which ranks by pgvector cosine distance in SQL (score = 1 − distance).
5. Builds each result's folder-path display via `FolderPathBuilder`, omitting the root "Home"
   segment (e.g. `"HR / Policies"`, not `"Home / HR / Policies"`), matching the requirements'
   example format.

`Search:MinSimilarity` (default `0.25`, 0 disables) drops chunks below that cosine similarity in SQL, so a query the
documents don't cover returns no results rather than the K nearest irrelevant chunks. The default is a starting guess
for `text-embedding-3-small`, **not tuned on real data** — adjust via user-secrets (`dotnet user-secrets set
"Search:MinSimilarity" "0.3"`) or `Search__MinSimilarity` when results look too sparse or too noisy.
`DocumentChunk.PageNumber` (PDFs) and `DocumentChunk.SheetName` (XLSX) locate a chunk in its source:
`ITextExtractor` returns `ExtractedSection`s (one per non-blank PDF page, one per non-empty XLSX worksheet, a single
location-less section for DOCX/TXT/CSV) and `TextChunker` chunks each section separately, so a chunk never spans a
page/sheet and overlap does not carry across the boundary. At most one of the two is set (DB CHECK constraint
`CK_DocumentChunks_PageOrSheet`). DOCX has no location (no reliable page boundaries); documents uploaded before
these changes have nulls — re-upload to populate.

Similarity/ranking/top-K run in PostgreSQL via pgvector. The OpenAI SDK is used for embeddings and, in the Ask flow,
for chat completions (no Semantic Kernel, no LangChain).

### Ask (RAG) flow

`POST /api/answers/stream` → `AnswerService.StartAsync` (validate → rewrite follow-up to a standalone question if there
is history → `ISearchService` overload with `SearchTuning(Rag:MaxContextChunks, Rag:MinSimilarity)` → `ContextSelector`
numbers sources 1..N) then `AnswerRun.StreamAsync` (no sources ⇒ `notfound`, no LLM call; otherwise
`IChatCompletionService.StreamAsync` through `AnswerStreamProcessor`, which detects the leading `NOT_FOUND` sentinel and
validates `[n]` citations). Everything that can fail with an HTTP status happens in `StartAsync`, before the first SSE
byte (ProblemDetails via the middleware: 400/404/502/503); later failures are an `error` event. The Application layer
yields `AnswerEvent`s; only `AnswersController` + `Streaming/SseWriter` know about HTTP/SSE. The conversation lives in
the browser (`useAsk`) and is sent as `history` each request; the server is stateless and never logs questions,
chunks, prompts or answers. Answers are rendered as plain text (no Markdown). `/api/answers` is rate-limited per IP
(sliding window + concurrent-stream cap, `Rag:RateLimit`); `GET /api/settings` exposes only client-safe limits.
`Rag:Temperature` / `Rag:MaxOutputTokens` are sent to the model only when set. **Phase 0 (real-model compatibility
spike for `OpenAI:ChatModel`) and Phase 6 manual QA have not been run** — see rag.md §16.

### Frontend architecture

```
citadel-iq-ui/src/
 ├─ app/            AppShell (layout + search panel host), TopNavigation, Footer, FloatingShapes,
 │                   FolderPage (route)
 ├─ components/
 │   ├─ folders/    Breadcrumbs, FolderCard, CreateFolderDialog
 │   ├─ documents/  FileCard, FileIcon, ProcessingStatusIcon
 │   ├─ upload/     UploadDialog, UploadDropzone
 │   ├─ search/     SearchPanel, SearchScopeSelector, SearchInput, SearchResults, SearchResultCard
 │   └─ common/      EmptyState, LoadingState, ToastProvider, ConfirmDialog, GlassSurface,
 │                    IconBadge, SectionHeader, AssistantInfoPanel
 ├─ hooks/          useFolderContents, useCreateFolder, useRenameFolder, useDeleteFolder,
 │                   useDeleteDocument, useUpload, useSearch
 ├─ api/            apiClient (fetch wrapper + ApiError), foldersApi, documentsApi, searchApi
 ├─ theme/          ColorModeProvider (light/dark, persisted to localStorage), theme.ts, glass.ts
 ├─ types/          folder.ts, search.ts — hand-kept in sync with backend DTOs
 └─ utils/          highlightMatches.tsx — best-effort literal term highlighting in search snippets
```

**Ask UI**: `SearchPanel` is the drawer shell with an **Ask | Passages only** toggle (Ask is hidden when
`GET /api/settings` says `askEnabled: false`; `SettingsProvider` fetches once). `hooks/useAsk` holds the whole
conversation, builds `history` (answered + "not found" turns; error/aborted turns omitted), enforces the session limit
and aborts the stream on close/new session; `api/sse.ts` is the `fetch`-based SSE client. `components/ask/` renders
question → answer (`AnswerText`: plain text, valid `[n]` markers as chips, invalid dropped) → `SourceList` (cited cards
built on `SearchResultCard` with a Download button; others collapsed).

**Visual design — glassmorphism**: a diagonal gradient page background (`theme/glass.ts`'s
`glossyBackground(mode)` — a fixed palette in dark mode, a pastel equivalent in light mode) with
soft floating blurred shapes (`FloatingShapes`,
`position: fixed`, `zIndex: -1`) drifting behind everything. Every card, panel, and bar
(`FolderCard`, `FileCard`, `SearchResultCard`, `TopNavigation`, `Footer`, the search drawer, the
Documents/Subfolders panel) is built from the shared `GlassSurface` component
(`components/common/GlassSurface.tsx` + `theme/glass.ts`'s `glassSurfaceSx`) — a translucent,
backdrop-blurred surface — rather than plain MUI `Paper`, so the look stays consistent. `FolderPage`
renders one `GlassSurface` panel containing two sections (Documents, then Subfolders), each with a
`SectionHeader` (icon + title + count chip); processing status on a `FileCard` is a small inline
`ProcessingStatusIcon` (spinner → checkmark/error), not a text chip, to keep cards as simple as
`FolderCard`. Card actions are inline `IconButton`s, not an overflow menu: `FileCard` shows
Download + Delete, `FolderCard` shows Open + Rename + Delete. Delete on either card opens a shared
`ConfirmDialog` (`components/common/ConfirmDialog.tsx`) whose description text differs per case —
the folder version explicitly warns that all subfolders and documents in the subtree will go with
it — before calling `DELETE /api/folders/{id}` or `DELETE /api/documents/{id}`. `FolderCard`'s
Rename swaps the name into an inline `TextField` and swaps Open+Rename+Delete for Save + Cancel
icons (Enter also saves, Escape also cancels) — the card's own `onClick` navigation is disabled
while editing so clicking the textbox doesn't navigate into the folder.

**Action icon colors** (`theme/glass.ts`'s `actionIconButtonSx(color)` — a tinted circular
background at rest, stronger on hover, muted while disabled): every file type's icon/badge and the
Download button share one accent, `theme.palette.success.main` (there's no longer a
color-per-extension palette); Open/Rename and the folder icon/badge share the theme's indigo,
lightened for dark mode via `theme/glass.ts`'s `folderAccentColor(theme)` (`#4338ca` light /
`#a5b4fc` dark — matches `MuiButton`'s `outlined` override in `theme.ts`, since the flat
`#4f46e5` primary tone is too low-contrast against the near-black glass surface); Delete and
`ConfirmDialog`'s confirm button use `theme.palette.error.main`. Deliberately three distinct hues
(green / indigo / red) so same-row actions don't blend together.

**Persistent sidebar** (`components/common/AssistantInfoPanel.tsx`, mounted once in `AppShell`, not
per-page): an evergreen "ask your documents, get cited answers" (RAG) panel — copy is deliberately *not* a one-time "Welcome"
message, since it's always visible, never remounts on navigation. It's a genuine CSS Grid column
(`AppShell`'s root `display: grid`, `gridTemplateColumns: '300px 1fr'` from `md` up), `position:
sticky` so it stays in view while scrolling, and `display: none` below `md` — there's no good place
to stack it on a narrow screen without competing with the header, so it's a desktop-only extra.
**Gotcha already hit once:** don't give `TopNavigation`/`Footer`/`<main>`'s inner content wrapper
`mx: 'auto'` centering *inside* the content column now that the sidebar is permanent — the column
is wider than the 1320px content cap on most screens, so `mx: 'auto'` spreads that leftover space
as a gap on both sides that grows with the window. Cap the width but don't center; let excess space
fall to the right instead.

Notes:
- `FolderPage`'s toolbar shows a **Back** button (before "New folder") whenever the current folder
  isn't Home, navigating to `contents.folder.parentFolderId` — added because relying on the
  breadcrumbs alone to go up a level isn't discoverable for every user.
- `useSearch`'s default `SearchScope` is `CurrentFolder` ("This folder"), not `EntirePortal` — and
  `SearchScopeSelector`'s `OPTIONS` array orders them This folder → +Subfolders → Entire portal,
  so the default matches the first/leftmost toggle button.
- `AppShell` derives the "current folder" for the search panel via `useParams()` — React Router
  v6 merges params from the whole matched route branch, so this works even though `AppShell` is
  the parent layout route and `folderId` is defined on the child route.
- MUI resolved to **v9.4.0** — its `Box`/`Stack`/`Typography` do **not** accept layout props
  (`display`, `gap`, `fontWeight`, etc.) directly; everything must go through the `sx` prop. All
  components in this codebase already follow this; if you copy an example from older MUI docs
  that uses `<Box display="flex">`, it will fail to typecheck here.
- **Tailwind v4 has no JS-config way to disable Preflight** (`corePlugins.preflight: false` in
  `tailwind.config.js` is silently ignored — v4 dropped that option). Preflight is disabled instead
  by importing `tailwindcss/theme.css` + `tailwindcss/utilities.css` directly in `index.css` rather
  than the full `tailwindcss` entrypoint, so it never fights MUI's own `CssBaseline` reset.
- The header (`TopNavigation`) is a plain glass bar, **not** `position: sticky` — deliberately, to
  avoid a real CSS trap: an ancestor with `overflow-x: hidden` (used for the old decorative-blob
  layer) silently turns into a scroll container when its `overflow-y` is left `visible` (browsers
  coerce the mismatched axis to `auto`), which breaks `position: sticky` on any descendant. Keep
  decorative overflow-hidden layers `position: absolute`/`fixed` and out of the ancestor chain of
  anything that needs to stick.
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

**PostgreSQL with the pgvector extension** must be running (e.g. the `pgvector/pgvector` Docker image). In
Development the API applies FluentMigrator migrations on startup. Set the connection string (the committed value is empty):
```bash
dotnet user-secrets set "ConnectionStrings:CitadelIQ" "Host=localhost;Port=5432;Database=citadeliq;Username=postgres;Password=..."
```

**Azure Blob Storage (optional)** instead of local disk:
```bash
dotnet user-secrets set "Storage:Provider" "AzureBlob"
dotnet user-secrets set "Storage:AzureBlob:AccountName" "<account>"
dotnet user-secrets set "Storage:AzureBlob:ContainerName" "documents"
dotnet user-secrets set "Storage:AzureBlob:ConnectionString" "<from the portal>"   # or omit and use `az login` / managed identity
```
Create the storage account and a **private** container once (portal or `az`); files already on local disk are not migrated.

**Required secrets** (never in `appsettings.json` — that file is committed to git):
```bash
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
dotnet user-secrets set "OpenAI:EmbeddingModel" "text-embedding-3-small"
```

### Local database (Docker) and migrations

Local dev runs PostgreSQL via `docker run ... pgvector/pgvector:pg17` with a named volume (exact commands,
`psql` usage and reset steps are in [README.md](./README.md)). The data lives in the volume; uploaded files
live separately in `App_Data/documents` — reset both together.

**Adding a schema change:** add a new numbered class to `CitadelIQ.FluentMigrations/Migrations/`
(`[Migration(<next number>)]`; use `Execute.Sql` for pgvector/expression-index bits), then update the matching
EF configuration in `CitadelIQ.Infrastructure/Persistence/Configurations/` by hand — nothing verifies the two
agree, and a mismatch only shows up as a query error. The migration runs at startup when
`Database:MigrateOnStartup` is true (set in `appsettings.Development.json`). The embedding dimension
(`vector(1536)`) is hardcoded in the first migration; changing the model/dimension needs a new migration and
re-embedding.

### Docker (deployment)

`docker-compose.yml` runs `pgvector/pgvector:pg17` (pinned — a major bump won't start on an existing volume),
the API (`CitadelIQ.Api/Dockerfile`, build context = repo root) and the UI (`citadel-iq-ui/Dockerfile` →
nginx). Copy `.env.example` to `.env` (`POSTGRES_PASSWORD`, `OPENAI_API_KEY`, optional `OPENAI_EMBEDDING_MODEL`,
`PUBLIC_API_URL`, `UI_ORIGIN`). The API receives `ConnectionStrings__CitadelIQ`, `OpenAI__ApiKey`,
`Cors__AllowedOrigins__0` and `Database__MigrateOnStartup=true` as environment variables; Postgres data and
uploaded files persist in the `pgdata` and `documents` volumes (back up both together; with `Storage:Provider=AzureBlob` the files live in the blob container instead — back up the DB and enable soft delete/versioning on the container). `VITE_API_URL` is baked
into the UI at build time. Postgres is not published to the host. The app has no auth: don't expose it publicly.
These Docker files have not yet been built/run end-to-end.

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
| `ConnectionStrings` | `CitadelIQ` | *(empty — user-secrets / env var only)* | PostgreSQL connection string; the app refuses to start if it's empty |
| `OpenAI` | `EmbeddingDimension` | `1536` | Size of the pgvector column — must match the model; changing it needs a new FluentMigrations migration (the migration hardcodes `vector(1536)`) |
| `OpenAI` | `ApiKey` | *(user-secrets only)* | Never in `appsettings.json` |
| `Upload` | `MaxFileSizeMB` | `20` | Rejected before processing starts |
| `Upload` | `AllowedExtensions` | `.pdf .docx .txt .csv .xlsx` | Configurable allow-list, not hardcoded |
| `Chunking` | `ChunkSize` / `ChunkOverlap` | `400` / `80` (tuned down from `800`/`150` — smaller chunks discriminate better between topically-similar documents) | Characters per chunk / overlap |
| `Search` | `DefaultTopK` / `MaxTopK` | `10` / `50` | Result count defaults/caps |
| `Search` | `MinSimilarity` | `0.25` | Minimum cosine similarity for a result (0 = off); untuned starting value, override via user-secrets |
| `OpenAI` | `ChatModel` | `gpt-5.6-luna` | Chat model for answers/rewrites (unverified — Phase 0) |
| `Rag` | `Enabled`, `MaxContextChunks`/`Cap`, `MaxContextChars`, `MinSimilarity` | `true`, `8`/`12`, `8000`, `0.30` | Kill switch (503), context size, answer similarity floor (untuned) |
| `Rag` | `MaxQuestionLength`, `MaxHistoryTurns`, `MaxHistoryChars`, `RewriteAnswerExcerptChars` | `1000`, `10`, `12000`, `500` | Hard input limits (server rejects, never truncates) |
| `Rag` | `MaxOutputTokens`, `Temperature` | unset | Sent to the model only when set |
| `Rag:RateLimit` | `PermitLimit`/`WindowSeconds`/`MaxConcurrentStreams` | `20`/`60`/`2` | Per-IP limits on `/api/answers` |
| `Storage` | `Provider` | `LocalDisk` | `LocalDisk` or `AzureBlob`; unknown values fail at startup |
| `Storage:AzureBlob` | `AccountName`, `ContainerName`, `ServiceUri`, `CreateContainerIfMissing` | `""`, `documents`, unset, `false` | Account/container are config. `ConnectionString` is a **secret** (user-secrets / `Storage__AzureBlob__ConnectionString`), takes precedence over Entra ID |
| `Storage` | `DocumentsPath` | `App_Data/documents` | Relative to `IHostEnvironment.ContentRootPath` — resolved once at host startup, not `Directory.GetCurrentDirectory()` (which depends on how the process was launched) |

## API contract

```
GET    /api/folders/root                    # well-known Home folder id (Guid.Empty)
GET    /api/folders/{folderId}              # folder metadata
GET    /api/folders/{folderId}/contents     # folder + breadcrumb (folderPath) + subfolders + documents
POST   /api/folders                         # { parentFolderId, name }
PUT    /api/folders/{folderId}              # { name } — rename
DELETE /api/folders/{folderId}              # cascades: all descendant subfolders + all documents in the subtree
POST   /api/documents/upload                # multipart: folderId, file
GET    /api/documents/{documentId}/status   # { id, status, failureReason }
GET    /api/documents/{documentId}/download # streams original file, original filename
DELETE /api/documents/{documentId}          # removes stored file, chunks, and embeddings
POST   /api/search                          # { query, currentFolderId, searchScope, topK? }
POST   /api/answers/stream                  # SSE: question, sources, text*, [notfound], done | error — body { question, currentFolderId, searchScope, history[] }
GET    /api/settings                        # client-safe limits: askEnabled, maxQuestionLength, maxHistoryTurns, maxHistoryChars
GET    /health
GET    /health/storage                      # document-store reachability (503 if the Azure container is unreachable)
```

`DELETE /api/folders/{folderId}` rejects the well-known root ("Home") folder with a 400. Folder
deletion walks the full descendant tree server-side (`IFolderRepository.GetDescendantIdsAsync`,
already recursive) and deletes every document found anywhere in that subtree via
`IDocumentService.DeleteDocumentsAsync` before removing the folder records themselves. Embeddings are a
column on the chunk row, so deleting a document's chunks removes them too; chunks are deleted before the raw
file and document record, so a crash mid-delete never leaves orphaned chunks pointing at a missing document.
(The FK `ON DELETE CASCADE` rules in the schema are a backstop, not the primary path.)

`PUT /api/folders/{folderId}` also rejects renaming the root ("Home") folder with a 400, and
rejects a name collision with a sibling (same `ExistsWithNameAsync` check `POST /api/folders`
uses), except when the new name differs only by case from the folder's current name.

`ProcessingStatus` and `SearchScope` enums serialize as PascalCase strings (JSON string enum
converter registered in `Program.cs`), e.g. `"searchScope": "CurrentFolderAndSubfolders"`.

## Important implementation principles (carry these into any change)

1. Keep the backend Clean Architecture — Application never references Infrastructure or Api;
   Domain never references anything.
2. Keep business logic out of controllers — they only map HTTP ↔ Application calls.
3. Every external dependency (OpenAI, disk, the database) sits behind an Application-layer
   interface — this is what made the Postgres/pgvector swap an Infrastructure-only change.
4. File-size/type limits and chunk size/overlap are configuration, not hardcoded — see the table
   above.
5. Never expose API keys, stack traces, or internal exception details in an API response — route
   all failures through `ExceptionHandlingMiddleware`'s known exception types, or accept the
   generic 500 message.
6. Async everywhere for I/O — file processing and OpenAI calls.
7. No video uploads, no separate vector database (pgvector inside PostgreSQL is the vector store), no
   Semantic Kernel, no LangChain, no RAG answer *persistence*/server-side conversations/Markdown rendering — these are documented future phases, not oversights.

## Out of scope for this version

Authentication/authorization, per-user document spaces,
server-side conversation history, search history/pagination, hybrid keyword search, file rename, move,
bulk operations, document previews, audit logging, background job queues (the current dispatcher
is in-process fire-and-forget, not a persistent queue). See [design.md](./.claude/technical-designs/design.md) §9 and §21 for the
full future-enhancements list and the reasoning behind each deferral. (Folder *rename* and
*deletion* — including cascade delete of a folder's subtree — are in scope; see the API contract
above.)

## Known gaps and conventions

- **Tests (`CitadelIQ.Tests`, xUnit):** `dotnet test CitadelIQ.slnx`. `Unit/` covers `TextChunker` and the text
  extractors (no Docker needed). `Integration/` starts a throwaway `pgvector/pgvector:pg17` container via Testcontainers
  (**Docker must be running**), gives every test its own freshly-migrated database, and exercises the real
  repositories/services: migrations, upload→process, page/sheet locations, failure cleanup, vector search ranking,
  scopes, `MinSimilarity`, Ready-only search, folder uniqueness (incl. concurrent creates) and cascade delete.
  Only OpenAI (`FakeEmbeddingService`, deterministic topic-axis vectors) and the background dispatcher (no-op;
  tests call `ProcessDocumentAsync`) are replaced. `Integration/AnswersApiTests` drive the real pipeline through `WebApplicationFactory` (SSE framing/order, error events, 400/404/503/429, settings) with `FakeChatCompletionService`; `Unit/RagUnitTests` cover the RAG pieces. No frontend tests yet.
- **Obsolete-but-kept code:** `InMemory*Repository` classes and `CosineSimilarity` are marked `[Obsolete]`,
  not registered in DI, and kept on purpose — don't delete them or wire them back in.
- **Stop hook** (`.claude/hooks/kill-stale-backend.sh`): kills the backend on `:5157` at the end of a turn only if
  a backend file (`.cs`/`.csproj`/`.slnx`/`appsettings*.json`) changed after that process started, so
  frontend-only edits leave it running; restart `dotnet run` after backend edits.
- `.claude/hooks/block-appsettings-secrets.py` blocks writing an OpenAI-key-looking value into
  `appsettings*.json`; `ConnectionStrings:CitadelIQ` is intentionally empty there.
