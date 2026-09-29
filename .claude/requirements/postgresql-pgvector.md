# Requirements — PostgreSQL + pgvector Persistence

> Distilled from `.claude/technical-designs/postgresql-pgvector.md` (the full implementation plan,
> phases, and Claude Code prompts live there — this file is just the "what," not the "how").

## Objective

Replace CitadelIQ's current in-memory storage (folders, documents, chunks, embeddings) with
durable PostgreSQL storage, using `pgvector` for embedding storage and similarity search, without
changing the public API contract or requiring a React rewrite.

## Functional requirements

- **Folders**
  - Persist `Id`, `Name`, `ParentFolderId`, `CreatedAtUtc`.
  - Exactly one root folder (`Guid.Empty`, named "Home") must exist after migration/seed — not an
    arbitrary set of root-level folders.
  - Enforce sibling-name uniqueness at the database level (`(ParentFolderId, lower(Name))` unique
    index), not just in application code.
  - Deleting a folder must remove all descendant subfolders and every document in that subtree.
- **Documents**
  - Persist `Id`, `FolderId`, `FileName`, `ContentType`, `SizeBytes`, `UploadedAtUtc`,
    `ProcessingStatus`, `FailureReason` — status/failure-reason are required fields, not optional,
    since `GET /api/documents/{id}/status` polling depends on them.
  - Deleting a document must remove its stored file, chunks, and embeddings.
- **Document chunks + embeddings**
  - Persist `Id`, `DocumentId`, `ChunkIndex`, `Text`, `PageNumber` (nullable), `Embedding`,
    `ModelName`.
  - One embedding per chunk — never one embedding for a whole document.
  - Embedding and model name live on the same table/row as the chunk (not a separate joined
    table), so a similarity query never needs a join to rank a row.
- **Vector search**
  - Similarity ranking (`ORDER BY ... LIMIT topK`) must be pushed down into PostgreSQL/pgvector —
    not loaded into memory and computed in C#, as today.
  - Must still support all three existing search scopes (Entire Portal / Current Folder / Current
    Folder + Subfolders), with folder-tree resolution staying in the Application layer.
  - Must filter to `ProcessingStatus.Ready` and respect `SearchOptions.MaxTopK` at the query level.
- **Migrations**
  - EF Core migration creates all tables, relationships, indexes, and enables the `pgvector`
    extension.
  - Original files stay on disk (or future blob storage) — never stored in a database column.

## Non-functional requirements

- Clean Architecture boundaries preserved: Domain has no EF Core/PostgreSQL/pgvector/OpenAI
  dependency; Infrastructure is the only layer that knows about any of them.
- No hard-coded secrets — connection strings via User Secrets (dev) / environment variables
  (deployed), same pattern as the existing `OpenAI:ApiKey`.
- The existing fire-and-forget background dispatcher must resolve a fresh, scoped `DbContext` per
  background task — never share one across the HTTP request and the background `Task.Run`.
- Data must survive an application restart (the core proof this migration actually works).
- Existing API contracts and the React UI continue working unchanged wherever practical.

## Out of scope for this phase

RAG/LLM-generated answers, authentication/authorization, multi-tenancy, cloud blob storage,
background job queues/workers, hybrid keyword+vector search, advanced ANN index tuning, document
versioning, video processing.

## Definition of done

- [ ] PostgreSQL connected; `pgvector` extension enabled.
- [ ] EF Core migration applied; all tables/relationships/indexes exist.
- [ ] Folder create/rename/delete (including cascade) works against Postgres.
- [ ] Upload → extract → chunk → embed → persist works end-to-end.
- [ ] Search returns correct, scoped, ranked results via pgvector (not in-memory cosine similarity).
- [ ] Data survives an API restart.
- [ ] No secrets committed; solution builds; existing tests pass.
