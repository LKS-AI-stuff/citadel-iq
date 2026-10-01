# CitadelIQ — Implementation Plan

Status: **All 7 build-order phases complete** — scaffold, folder browsing, upload/processing
pipeline, search, polish pass, documentation (README.md/CLAUDE.md), and a post-launch glassmorphism
UI redesign (§10 item 7). Verified end-to-end with a real OpenAI key. Remaining work is everything
explicitly deferred to "future scope" (§9, §18–21): auth, PostgreSQL/pgvector, RAG, and the
document-management/enterprise features listed there.
Source of truth for requirements: `Requirements.pages` (23 sections, reviewed in full).

## 1. What we're building

A professional, full-stack **document management + semantic search portal** — explicitly *not* a
chatbot. Users upload documents into folders, browse them like a file manager, and run
natural-language searches that return the most relevant document chunks ranked by cosine
similarity. No AI-generated answers yet (that's a documented future phase — RAG).

The current version exists to clearly demonstrate the embeddings → chunking → similarity
pipeline, wrapped in an enterprise-grade UI, on an architecture that won't need rework when
Postgres/pgvector, auth, and RAG are added later.

## 2. Tech stack

| Layer | Choice |
|---|---|
| Backend | ASP.NET Core Web API (.NET 10), C# |
| Backend architecture | Clean Architecture (Domain / Application / Infrastructure / Api) |
| Frontend | React 19 + TypeScript + Vite |
| UI | Material UI (MUI) for components/accessibility, Tailwind CSS for layout/spacing/utilities |
| AI | Official OpenAI .NET SDK, `text-embedding-3-small` |
| Similarity/ranking | Implemented in-app (cosine similarity, top-K, ranking) — no vector DB yet |
| Storage (current) | Global, app-wide in-memory store behind repository interfaces |
| Text extraction | PdfPig (PDF), DocumentFormat.OpenXml (DOCX), ClosedXML (XLSX), plain read (TXT/CSV) |
| Version control | New git repo in `citadel-iq/`, mirroring the `ChatWithTheBot` reference project |

Reference project (`../ChatWithTheBot`) confirms this stack is proven on this machine (.NET 10
SDK and modern Node are already installed) — its README/CLAUDE.md pattern (user secrets for the
API key, `.env.local` for the frontend, CORS allow-list) will be reused here.

## 3. Decisions made during planning (with rationale)

- **Storage scope — global in-memory singleton**, not session/cookie-scoped. There's no auth yet,
  so per-session isolation would be throwaway complexity that gets replaced by real user isolation
  later anyway. A singleton behind `IDocumentRepository`/`IFolderRepository`/`IEmbeddingStore` is
  trivial to swap for Postgres+pgvector.
- **Text extraction libraries**: PdfPig + DocumentFormat.OpenXml + ClosedXML — all MIT/permissive,
  no native dependencies, actively maintained.
- **Git**: initialize a fresh repo in `citadel-iq/` now, with a `.gitignore` matching the
  reference project's (bin/obj, node_modules, .env.local, user secrets).
- **No seed data**: the app starts with an empty `Home`. Folder creation is a first-class UI
  feature (see §7) — folders and documents are created by you while testing, nothing is
  pre-populated.
- **Repository Pattern, explicitly, backed by in-memory dictionaries** (not EF Core, not a real
  database, for now). `IDocumentRepository`, `IFolderRepository`, `IEmbeddingStore` are real
  interfaces with `ConcurrentDictionary`-backed implementations. EF Core + FluentMigrator +
  Postgres were considered and deliberately deferred — see rationale below.
- **OpenAI configuration**: both the API key *and* the embedding model name are configuration
  values, not hardcoded — set via `dotnet user-secrets` in dev (`OpenAI:ApiKey`,
  `OpenAI:EmbeddingModel`) and environment variables in other environments, mirroring the
  reference project's pattern. Defaults to `text-embedding-3-small` if unset, but is fully
  swappable without a code change.
- **Defaults for configurable values** (all overridable via `appsettings.json`):
  - `MaxFileSizeMB`: 20
  - `AllowedExtensions`: `.pdf, .docx, .txt, .csv, .xlsx`
  - Chunk size: 400 characters with 80 character overlap (tuned down from an initial ~800/~150 —
    smaller chunks discriminate better between topically-similar documents; configurable)
  - `TopK` default: 10

> **Update:** this deferral has since been lifted — see [postgresql-pgvector.md](./postgresql-pgvector.md).
> Storage is now PostgreSQL + pgvector, queried through EF Core, with the schema owned by
> FluentMigrator migrations in the separate `CitadelIQ.FluentMigrations` project. The reasoning
> below is kept as the historical record of why it was deferred originally.

### Why EF Core / FluentMigrator / Postgres are deferred, not adopted now

These were discussed and intentionally **not** adopted for this phase:
- Introducing a real database now (even without pgvector) means standing up Postgres locally,
  writing FluentMigrator migrations, and mapping EF Core entities — real setup cost for a phase
  whose stated purpose (per the requirements doc) is to demonstrate the embeddings/chunking/
  similarity mechanics, not persistence.
- The Repository Pattern already isolates the Application layer from the storage mechanism. Since
  the interfaces are identical either way, swapping the in-memory dictionary implementations for
  EF Core/Postgres/FluentMigrator-managed schema later is a pure Infrastructure-layer change — no
  rework, just a deferred one.
- When that swap happens, the natural shape is: FluentMigrator migrations define the schema, EF
  Core (or Dapper) implements the repository interfaces against Postgres, and embeddings move from
  an in-memory `float[]` to a `pgvector` column — all without touching Domain or Application.

## 4. Backend — Clean Architecture

```
CitadelIQ.sln
 ├─ CitadelIQ.Domain          (no dependencies)
 ├─ CitadelIQ.Application     (depends on Domain)
 ├─ CitadelIQ.Infrastructure  (depends on Application, Domain)
 └─ CitadelIQ.Api             (depends on Application, Infrastructure)
```

### Domain
Entities / value objects, no external dependencies:
- `Folder` (Id, Name, ParentFolderId, Path/breadcrumb helper)
- `Document` (Id, FolderId, FileName, ContentType, SizeBytes, UploadedAtUtc, ProcessingStatus)
- `DocumentChunk` (Id, DocumentId, ChunkIndex, Text, PageNumber?)
- `DocumentEmbedding` (ChunkId, float[] Vector, ModelName)
- `ProcessingStatus` enum: `Uploaded → ExtractingText → Chunking → GeneratingEmbeddings → Ready → Failed`
- Domain rules: filename validity, folder nesting rules, chunk ordering invariants.

### Application
Use cases / interfaces / DTOs, orchestrates domain + infrastructure abstractions:
- Commands/Queries: `UploadDocumentCommand`, `CreateFolderCommand`, `GetFolderContentsQuery`,
  `SearchDocumentsQuery`, `DownloadDocumentQuery`, `ProcessDocumentCommand` (internal pipeline step)
- Interfaces (implemented in Infrastructure): `IDocumentRepository`, `IFolderRepository`,
  `IEmbeddingStore`, `IOpenAIEmbeddingService`, `ITextExtractor` (+ per-type strategy),
  `IFileValidator`, `IDocumentStorage` (raw bytes)
- `SearchService`: owns cosine similarity, ranking, top-K, and **scope filtering** (Entire Portal /
  Current Folder / Current Folder + Subfolders) — the frontend only sends `currentFolderId` +
  `searchScope`; all filtering logic lives server-side per the requirements.
- Validation: file type/size checks (reads from configuration, not hardcoded), empty-query checks,
  empty-document checks.

### Infrastructure
- `InMemoryFolderRepository`, `InMemoryDocumentRepository`, `InMemoryEmbeddingStore` — thread-safe
  (`ConcurrentDictionary`-backed) singletons implementing the Application interfaces. Isolated
  behind interfaces so a future `PostgresDocumentRepository` / `PgVectorEmbeddingStore` drops in
  without touching Application.
- `LocalDiskDocumentStorage` implements `IDocumentStorage`: raw uploaded file bytes are written to
  a configurable folder on disk (`Storage:DocumentsPath` in `appsettings.json`, defaulting to
  `App_Data/documents` next to the API project — **not** the `bin/` build output folder, since
  `dotnet build`/`clean` wipes and regenerates `bin/` and would silently delete uploaded files on
  every rebuild). Each file is saved as `{documentId}{original-extension}` so raw bytes never
  compete with the `Document`/`Folder`/`DocumentChunk`/`DocumentEmbedding` metadata for RAM — only
  metadata, chunk text, and embeddings stay in the in-memory dictionaries. Download streams the
  file straight from disk under the original filename. `App_Data/` is added to `.gitignore` (it's
  runtime data, not source). Restarting the API still loses the in-memory metadata/chunks/
  embeddings (per §3), but the raw files themselves survive on disk until you delete them — so
  after a restart the app would show an empty folder tree while orphaned files remain in
  `App_Data/documents` until a future cleanup/reconciliation step. When the persistence layer is
  swapped in later, this becomes object storage (or a DB blob column) behind the same
  `IDocumentStorage` interface — no Application-layer changes.
- `OpenAIEmbeddingService` — wraps the official OpenAI .NET SDK, calls `text-embedding-3-small`.
- Text extractors: `PdfTextExtractor` (PdfPig), `DocxTextExtractor` (OpenXml), `XlsxTextExtractor`
  (ClosedXML), `PlainTextExtractor` (TXT/CSV) — selected via a small factory keyed by extension.
- `FileTypeValidator` reading `AllowedExtensions`/`MaxFileSizeMB` from `IOptions<UploadSettings>`.

### Api
- Controllers/minimal APIs are thin — just map HTTP ↔ Application commands/queries.
- DI registration (`Program.cs`), CORS for the Vite dev origin, `IOptions<T>` config binding,
  global exception handling middleware that converts exceptions to safe, user-friendly
  `ProblemDetails` (never leaking stack traces, API keys, or internals — required by §17).
- Endpoints:
  ```
  GET    /api/folders/{folderId}            # folder metadata + breadcrumb
  GET    /api/folders/{folderId}/contents   # child folders + files
  POST   /api/folders                       # create folder
  PUT    /api/folders/{folderId}            # rename folder
  DELETE /api/folders/{folderId}            # cascades to all descendant subfolders + their documents
  POST   /api/documents/upload              # multipart upload -> triggers processing pipeline
  GET    /api/documents/{documentId}/download
  DELETE /api/documents/{documentId}
  POST   /api/search                        # { query, currentFolderId, searchScope, topK }
  GET    /api/documents/{documentId}/status # poll processing status (for UI progress steps)
  ```
- OpenAI key stored via `dotnet user-secrets` (dev) — never sent to the frontend, matching the
  reference project's pattern.

## 5. Document processing pipeline

```
Upload → Validate (type + size) → Extract text → Split into chunks (size/overlap configurable)
  → Generate embedding per chunk (OpenAI, parallelized with throttling)
  → Store {Document, Chunks, Embeddings} in memory → Status: Ready
```
- Status is persisted per-document and exposed via `GET /api/documents/{id}/status` so the UI can
  show: `Uploading… → Extracting text… → Creating searchable sections… → Generating embeddings… →
  Ready to search`.
- Failure at any stage sets `ProcessingStatus.Failed` with a safe, user-facing reason (no
  exception detail).
- All I/O (extraction, OpenAI calls) is async; embeddings generated per-chunk, never one embedding
  for a whole document (§6 requirement).

## 6. Search flow

1. Frontend sends `{ query, currentFolderId, searchScope, topK }` to `POST /api/search`.
2. Backend embeds the query (OpenAI), resolves the eligible document set from `searchScope` +
   `currentFolderId` (Entire Portal / Current Folder / Current Folder + Subfolders — subfolder
   resolution walks the folder tree server-side).
3. Cosine similarity computed against every chunk embedding in the eligible set.
4. Results sorted by similarity descending, top-K returned with: document name, folder path,
   chunk text, similarity score (labeled explicitly, not framed as a probability), chunk number,
   page number (if available), file type.
5. No LLM call in this step — pure vector math, per §11/§20.

## 7. Frontend architecture

*This is the original pre-build proposal. The component tree and visual design evolved during
build and, later, a glassmorphism redesign (§10 item 7) — see CLAUDE.md's "Frontend architecture"
section for the current file tree and design-system notes.*

```
src/
 ├─ app/            AppShell, TopNavigation, routing
 ├─ components/
 │   ├─ folders/    Breadcrumbs, FolderView, FolderCard, FileList, FileCard, FileIcon,
 │   │               CreateFolderDialog
 │   ├─ upload/     UploadDialog, UploadDropzone, ProcessingStatus
 │   ├─ search/     SearchPanel (slide-over, ~half width), SearchScopeSelector,
 │   │               SearchInput, SearchResults, SearchResultCard
 │   └─ common/      EmptyState, LoadingState, ErrorMessage, ToastProvider
 ├─ hooks/          useFolders(), useDocuments(), useUpload(), useSearch(), useCreateFolder()
 ├─ api/            typed fetch client (axios or fetch wrapper), one module per resource
 ├─ types/          shared TS types mirroring backend DTOs
 └─ theme/          MUI theme + Tailwind config kept in sync (single design-token source)
```
- Home is the default route; folder navigation updates the URL and breadcrumb but keeps the same
  shell/layout (§3 — no route should look like "a different app").
- **Folder creation is a first-class, always-available action**: a "New Folder" button sits
  alongside the Upload button in the toolbar at every level of navigation (not just Home).
  `CreateFolderDialog` collects a name, validates it client- and server-side (non-empty, no
  duplicate sibling name, no path-breaking characters), and creates the folder as a child of
  whatever folder is currently being viewed via `POST /api/folders { name, parentFolderId }`.
  The upload flow also offers "create a new folder" inline as a destination option, so a user can
  create-and-upload in one motion instead of two separate steps.
- Search panel is a MUI `Drawer` anchored right, ~50% width, dismissible, overlaying without
  hiding the document view underneath.
- No chat-style UI anywhere — file/folder cards, breadcrumbs, drag-and-drop, skeleton loaders,
  toasts, and context menus are the primary UI language (§15).

## 8. Error handling (§17)

Centralized API error middleware maps known failure cases to friendly messages:
unsupported file type, file too large, empty document, text-extraction failure, OpenAI failure,
search-before-ready, empty query, folder/document not found, download failure. Frontend renders
these via toast/inline `ErrorMessage` components — never raw error objects.

## 9. Explicitly out of scope for this version (per requirements §18–21)

Authentication/authorization, per-user document spaces, PostgreSQL/pgvector, EF Core/FluentMigrator
(see §3 rationale), Semantic Kernel, LangChain, RAG/LLM-generated answers, search
history/pagination, hybrid keyword search, file rename, move, bulk operations,
document previews, audit logging, background job queues. (Folder *creation*, *rename*, and
*deletion* are in scope — see §7 and §10 items 8–9.)
The architecture (interfaces around storage + embeddings) is deliberately shaped so all of these
can be added later without reworking the Application layer.

## 10. Build order

1. ✅ Solution scaffold: 4 backend projects wired with correct references + DI skeleton; React +
   Vite + TS + MUI + Tailwind app shell with routing and empty-state Home screen.
2. ✅ Folder/file browsing UI against a minimal in-memory folder API — navigation, breadcrumb
   (`FolderPathSegmentDto`), and create-folder are live end-to-end. AutoMapper replaced the manual
   `ToDto` methods for entity→DTO mapping.
3. ✅ Upload pipeline: validation → extraction (PdfPig/OpenXml/ClosedXML) → chunking → OpenAI
   embeddings → in-memory metadata/chunk/embedding storage, with raw file bytes on local disk
   (`App_Data/documents`, not RAM, not `bin/`). Processing runs via an in-process fire-and-forget
   dispatcher (`IDocumentProcessingDispatcher` / `BackgroundDocumentProcessingDispatcher`) so the
   upload call returns immediately with status `Uploaded`, and the frontend polls
   `GET /api/documents/{id}/status` (via a 2s interval in `FolderPage`) to reflect
   `ExtractingText → Chunking → GeneratingEmbeddings → Ready/Failed` live in the file grid.
   Verified end-to-end via curl: upload → disk file written → text extracted → chunked → fails
   safely with a generic message when no OpenAI key is configured (real key needed to reach
   `Ready`).
4. ✅ Search: backend cosine-similarity/scope logic (`SearchService`, `CosineSimilarity`) +
   `POST /api/search`, plus the slide-over `SearchPanel` (MUI `Drawer`, ~50% width), scope
   selector (radio buttons), and result cards showing similarity score/chunk/page/folder path.
   Verified: empty-query validation, unsupported-file-type rejection, and CORS all confirmed via
   curl against the running API.
5. ✅ Polish pass: light/dark theme toggle (`ColorModeProvider`, persisted to localStorage),
   contextual-action menus on folder/file cards (`CardActionsMenu`), search-result term
   highlighting (`highlightMatches`), toast notifications when a document's background processing
   settles to `Ready`/`Failed` (not just within the upload dialog), and the real `FailureReason`
   now flows from `Document` through `DocumentSummaryDto` to the UI's status icon tooltip instead
   of a placeholder string.
6. ✅ README.md and CLAUDE.md written at the repository root, mirroring the reference project's
   format but reflecting CitadelIQ's actual architecture, endpoints, and configuration.
7. ✅ **UI redesign — glassmorphism theme**, done in a later session at the user's request, modeled
   on the HTML template in `.claude/templates/templatemo_592_glossy_touch`: diagonal gradient page
   background with floating blurred shapes, every surface (cards, header, footer, search
   drawer) rebuilt on a shared `GlassSurface` component instead of plain MUI `Paper`, the
   folder/file grid restructured into two sections (Documents, then Subfolders) inside one panel,
   and a permanent desktop-only sidebar (`SearchInfoPanel`) explaining semantic search — evergreen
   copy, not a one-time welcome message, since it never remounts on navigation. Two real bugs were
   found and fixed along the way, both now called out in CLAUDE.md's frontend notes since they're
   easy to reintroduce: (1) Tailwind v4 silently ignores `corePlugins.preflight: false` in JS
   config — Preflight is disabled by importing `tailwindcss/theme.css` + `utilities.css` directly
   instead; (2) an `overflow-x: hidden` ancestor (from an earlier decorative-blob layer) broke
   `position: sticky` on the header by turning the ancestor into a scroll container — resolved by
   keeping the header non-sticky and any decorative overflow-hidden layer `position:
   absolute`/`fixed`, out of the ancestor chain of anything that needs to stick.
8. ✅ **Folder and document deletion**, added in a later session at the user's request (originally
   listed as future scope in §9/§21, now implemented): `FolderCard`/`FileCard`'s overflow menu
   (`CardActionsMenu`) was replaced with direct `IconButton`s — Open + Delete on folders, Download +
   Delete on documents — each Delete opening a shared `ConfirmDialog` with case-specific copy.
   Deleting a document removes its stored file (`IDocumentStorage.DeleteAsync`), chunks, and
   embeddings before the metadata record itself, so nothing orphaned survives it. Deleting a folder
   (`FolderService.DeleteFolderAsync`) rejects the well-known root/Home folder, then walks the full
   descendant subtree via the already-recursive `IFolderRepository.GetDescendantIdsAsync`, deletes
   every document found anywhere in that subtree through `IDocumentService.DeleteDocumentsAsync`
   (a bulk variant added alongside single-document delete to avoid an N+1 lookup), and only then
   removes the folder records — so a subfolder nested several levels deep, and all of its
   documents, are cleaned up in one call.
9. ✅ **Folder rename**, added in the same later session (also originally future scope in §9):
   `FolderCard` gained a Rename `IconButton` (ordered Open → Rename → Delete) that swaps the folder
   name for an inline `TextField` and swaps the icon row for Save + Cancel icons (Enter also saves,
   Escape also cancels); the card's own click-to-navigate is disabled while editing. `Folder.Rename`
   reuses the same name validation as `Folder.Create`; `FolderService.RenameFolderAsync` rejects
   renaming the root/Home folder and rejects a name collision with a sibling via the same
   `ExistsWithNameAsync` check folder creation uses, except when the new name differs from the
   current one only by case.
10. ✅ **Post-delete/rename UI polish**, same session: action icons across `FileCard`/`FolderCard`
    moved off a flat/neutral style onto `theme/glass.ts`'s `actionIconButtonSx(color)` — a tinted
    circular background — with three consistent hues (green for file actions/Download, the theme's
    dark-mode-aware indigo for Open/Rename, red for Delete) instead of the original
    color-per-file-extension palette. `FolderPage` gained a **Back** button next to "New folder",
    shown only when the current folder isn't Home, since relying on the breadcrumbs alone to go up
    a level isn't discoverable for every user. `useSearch`'s default `SearchScope` changed from
    `EntirePortal` to `CurrentFolder`, and `SearchScopeSelector`'s option order changed to match
    (This folder → +Subfolders → Entire portal).

**Verified with a real OpenAI key** (set via `dotnet user-secrets` during this session): a
`.txt` document uploaded, extracted, chunked, embedded, and reached `Ready`; a semantic search for
"How many vacation days can an employee take?" correctly returned the chunk "Employees are
eligible for 15 days of annual vacation..." despite no literal word overlap — confirming the core
requirement end-to-end.

## 11. Confirmed setup details

- **No seed data** — you'll create folders and upload files through the portal itself once it's
  running.
- **OpenAI key**: you have one ready and will set it via `dotnet user-secrets` (`OpenAI:ApiKey`).
- **Embedding model**: also configuration-driven (`OpenAI:EmbeddingModel`), not hardcoded —
  defaults to `text-embedding-3-small` if unset.

Plan is confirmed — ready to scaffold per the build order in §10 whenever you give the go-ahead.
