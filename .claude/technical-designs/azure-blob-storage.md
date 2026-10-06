# CitadelIQ — Technical Design: Azure Blob Storage for Raw Document Files

> Source: a direct request ("store files in the cloud instead of on my machine; Azure account name and
> container name must be configuration"). There is no matching file in `.claude/requirements/`.
> Builds on the existing `IDocumentStorage` abstraction (see `design.md` §3) and the project rules in `CLAUDE.md`.

---

## 1. Objective

Store uploaded raw files in **Azure Blob Storage** instead of `App_Data/documents` on the API host, with the
**storage account name and container name supplied as configuration** (never hardcoded). Local disk stays
available as a configurable provider for development and tests. No Application, Domain, Api-contract or
frontend change is expected: the swap is meant to be Infrastructure-only.

---

## 2. Current state

| Area | What exists | Where |
|---|---|---|
| Abstraction | `IDocumentStorage` with `SaveAsync(documentId, extension, Stream)`, `OpenReadAsync(...)` → `Stream`, `DeleteAsync(...)`. Blobs are identified by `{documentId}{extension}`. | `CitadelIQ.Application/Interfaces/IDocumentStorage.cs` |
| Implementation | `LocalDiskDocumentStorage` (singleton) writes to `ContentRootPath/Storage:DocumentsPath`. `File.Delete` is silent when the file is missing. | `CitadelIQ.Infrastructure/Storage/LocalDiskDocumentStorage.cs`, registered in `Infrastructure/DependencyInjection.cs` |
| Options | `StorageOptions { DocumentsPath }`, bound from `Storage`. | `CitadelIQ.Application/Options/StorageOptions.cs` |
| Callers | `DocumentService`: upload saves bytes then inserts the record (deleting the file if the insert fails); `ProcessDocumentAsync` opens the file and passes the stream to `ITextExtractionService`; `DownloadAsync` returns the stream, which `DocumentsController.Download` returns via `File(...)`; `DeleteDocumentsAsync` deletes chunks, then the file, then the record. | `CitadelIQ.Application/Documents/DocumentService.cs` |
| Extractors | PdfPig, OpenXml and ClosedXML consume a `Stream`; they may need a **seekable** stream. | `CitadelIQ.Infrastructure/TextExtraction/*` |
| Deployment | Compose mounts a `documents` volume at `/app/App_Data/documents`; docs say to back up the DB volume and the documents volume together. | `docker-compose.yml`, `CitadelIQ.Api/Dockerfile` |
| Tests | Integration tests use the real `LocalDiskDocumentStorage` against a temp content root. | `CitadelIQ.Tests/Support/TestApp.cs` |

---

## 3. Decisions made (with rationale)

1. **Infrastructure-only change.** A new `AzureBlobDocumentStorage : IDocumentStorage` replaces the local
   one by configuration. `IDocumentStorage` is unchanged (it was designed for this).
2. **Provider chosen by config** (`Storage:Provider` = `LocalDisk` | `AzureBlob`, default `LocalDisk`).
   Default stays local so existing setups and the test suite keep working with no new config.
3. **Account and container are options, not constants:** `Storage:AzureBlob:AccountName` and
   `Storage:AzureBlob:ContainerName`. The endpoint is derived as `https://{AccountName}.blob.core.windows.net`.
   An optional `Storage:AzureBlob:ServiceUri` overrides the derived endpoint (for Azurite or sovereign clouds).
4. **Two authentication modes, selected by config; neither secret is committed.**
   - **Entra ID / managed identity (preferred in deployment):** `DefaultAzureCredential` against the
     account endpoint. No secret exists. The identity needs the **Storage Blob Data Contributor** role.
   - **Connection string (local dev convenience):** `Storage:AzureBlob:ConnectionString`, supplied only via
     user-secrets or the `Storage__AzureBlob__ConnectionString` environment variable. If it is set it wins;
     otherwise `DefaultAzureCredential` is used (which also covers `az login` locally).
   This lets "storage account key access" stay enabled while developing and be switched off once deployed.
5. **Keep streaming downloads through the API** for now. Signed (SAS) download URLs would save bandwidth but
   change the download endpoint and the frontend link and need a permission model that does not exist yet
   (no auth). Deferred explicitly; the abstraction leaves room for an optional `GetDownloadUrl` later.
6. **Reads return a seekable stream.** `BlobClient.OpenReadAsync` returns a seekable, buffered stream, which
   satisfies the extractors without copying the blob into memory first. Verify in Phase 1 with a PDF, DOCX and XLSX.
7. **Blob name = `{documentId}{extension}`**, same as today. No folder hierarchy in the blob name: folders
   are a database concept here and can be renamed or deleted without touching storage.
8. **Delete is idempotent:** `DeleteIfExistsAsync`, matching the local implementation's behaviour, so a retry
   after a partial failure does not throw.
9. **Container is not created implicitly in production code paths that could hide a misconfiguration.**
   A startup/health check verifies the container exists and is reachable; creation is done once by the operator
   (portal/CLI). An opt-in `Storage:AzureBlob:CreateContainerIfMissing` (default `false`) exists for dev/Azurite.

---

## 4. Proposed design by layer

### 4.1 Domain
No changes.

### 4.2 Application
Extend `StorageOptions` (still plain options, no Azure types):

```csharp
public class StorageOptions
{
    public string Provider { get; set; } = "LocalDisk";        // "LocalDisk" | "AzureBlob"
    public string DocumentsPath { get; set; } = "App_Data/documents";
    public AzureBlobOptions AzureBlob { get; set; } = new();
}

public class AzureBlobOptions
{
    public string AccountName { get; set; } = "";
    public string ContainerName { get; set; } = "documents";
    public string? ServiceUri { get; set; }                     // optional endpoint override
    public string? ConnectionString { get; set; }               // secret: user-secrets / env only
    public bool CreateContainerIfMissing { get; set; }
}
```

`IDocumentStorage` and `DocumentService` are unchanged.

### 4.3 Infrastructure
- `Storage/AzureBlobDocumentStorage.cs` (singleton, `BlobContainerClient` created lazily like the OpenAI
  clients so a missing/incorrect config only fails when storage is used, not at startup):
  - `SaveAsync`: `UploadAsync(stream, overwrite: true)`; set `Content-Type` from the extension if cheap
    (optional; the DB already holds the content type).
  - `OpenReadAsync`: `OpenReadAsync` → seekable stream. A missing blob raises `RequestFailedException` (404);
    map it to the same exception type the local implementation surfaces for a missing file so the
    existing behaviour is preserved.
  - `DeleteAsync`: `DeleteIfExistsAsync`.
  - Provider failures are logged by status/type only (no URIs with SAS, no connection string) and rethrown
    as a safe exception handled by the existing middleware as a 500.
- `DependencyInjection`: register `LocalDiskDocumentStorage` or `AzureBlobDocumentStorage` based on
  `Storage:Provider`. Unknown provider → fail fast at startup with a clear message.
- Packages: `Azure.Storage.Blobs`, `Azure.Identity`.
- Options validation at startup when `Provider = AzureBlob`: `ContainerName` non-empty and matches Azure's
  naming rules (3–63 chars, lowercase letters/digits/hyphens); either `ConnectionString` or `AccountName` present.

### 4.4 Api
- Bind `Storage` as today (the nested `AzureBlob` section binds automatically).
- Add the container check to `/health` (or a separate `/health/storage`) only when the Azure provider is active.
- `appsettings.json` gets non-secret defaults only (`Provider`, `AzureBlob.AccountName: ""`,
  `AzureBlob.ContainerName: "documents"`); `ConnectionString` is never committed.
- `.claude/hooks/block-appsettings-secrets.py` already blocks key-looking values in `appsettings*.json`;
  extend it to also block `AccountKey=` / `DefaultEndpointsProtocol=` connection strings.

### 4.5 Frontend
No changes (download URL and upload flow are unchanged).

---

## 5. Configuration reference

| Key | Default | Notes |
|---|---|---|
| `Storage:Provider` | `LocalDisk` | `LocalDisk` or `AzureBlob` |
| `Storage:DocumentsPath` | `App_Data/documents` | Local provider only |
| `Storage:AzureBlob:AccountName` | *(empty)* | Required for Entra ID auth; derives `https://{name}.blob.core.windows.net` |
| `Storage:AzureBlob:ContainerName` | `documents` | Created once by the operator; private access |
| `Storage:AzureBlob:ServiceUri` | *(unset)* | Optional override (Azurite, sovereign clouds) |
| `Storage:AzureBlob:ConnectionString` | *(unset)* | **Secret** — user-secrets / env var only; takes precedence over Entra ID |
| `Storage:AzureBlob:CreateContainerIfMissing` | `false` | Dev/Azurite convenience |

Local development example:
```bash
dotnet user-secrets set "Storage:Provider" "AzureBlob"
dotnet user-secrets set "Storage:AzureBlob:AccountName" "<account>"
dotnet user-secrets set "Storage:AzureBlob:ContainerName" "documents"
dotnet user-secrets set "Storage:AzureBlob:ConnectionString" "<from portal, or omit and use az login>"
```
`docker-compose.yml` passes `STORAGE_PROVIDER`, `AZURE_STORAGE_ACCOUNT`, `AZURE_STORAGE_CONTAINER` and
`AZURE_STORAGE_CONNECTION_STRING` as optional env vars (mapped to `Storage__...`); the `documents` volume is
only needed for the local provider.

---

## 6. Data model / API contract changes
None. No migration. No endpoint changes.

**Existing files are not migrated automatically.** Documents stored under the local provider are not visible to
the Azure provider (the DB row exists but the blob does not). Switching providers on a database that already has
documents needs a one-off copy of `App_Data/documents/*` into the container (same blob names); see Open questions.

---

## 7. Non-functional considerations

- **Security:** container is private (no anonymous access); TLS only; no secret in `appsettings.json`; prefer
  managed identity and then disable account-key access in the portal; never log SAS URIs or connection strings;
  least-privilege role (Blob Data Contributor scoped to the account or container).
- **Failure handling:** the upload flow already deletes the stored file if the DB insert fails; with a remote
  store both calls can fail independently, so a failed cleanup delete should be logged and left for a
  reconciliation job (orphan blobs are harmless but cost storage). Delete order stays chunks → file → record.
- **Performance/cost:** uploads now cross the network twice (browser → API → Azure) and processing re-reads the
  blob; both are acceptable at the 20 MB limit. Egress for downloads is billed; keep the API and storage in the
  same region. Storage cost is negligible at this scale (cents per month).
- **Async + cancellation:** all calls take the `CancellationToken`; no sync-over-async.
- **Observability:** counts/durations/status codes only, consistent with the RAG logging rule.
- **Config, not constants:** account, container, endpoint, provider are all options.

---

## 8. Testing strategy

- **Unit:** options validation (container naming rules, provider selection, missing account/connection string);
  provider selection in DI.
- **Integration against Azurite** (Testcontainers `mcr.microsoft.com/azure-storage/azurite`, Docker already
  required by the suite): upload → save → read (seekable, round-trips bytes) → delete → delete again (idempotent)
  → read missing blob (expected error). Run the existing upload→process and download flows with the Azure
  provider to prove PDF/DOCX/XLSX extraction works from the blob stream.
- The existing suite keeps `LocalDisk` and must pass unchanged.
- **Manual QA against real Azure:** upload each supported type, process, download, delete a document and a folder
  with documents, then confirm the blobs are gone in the portal; confirm managed identity works with key access
  disabled.

---

## 9. Open questions / risks

1. **Existing local files.** Is there data worth migrating? If yes, add a one-off CLI/script (copy
   `App_Data/documents` into the container); otherwise re-upload.
2. **Signed download URLs vs API streaming** (decision 5) — revisit when auth/per-user permissions arrive, since
   SAS URLs bypass the API's access checks unless issued per request.
3. **Seekability / large files.** If an extractor misbehaves with the buffered blob stream, fall back to copying
   to a temp file for extraction. Confirm in Phase 1.
4. **Orphan reconciliation.** A periodic job comparing blobs to document rows is out of scope but worth noting.
5. **Redundancy and backups.** LRS survives a disk failure but not a region loss; consider GRS and blob soft
   delete / versioning when the data matters. Backup guidance in the docs changes from "back up two volumes" to
   "back up the database and enable soft delete on the container".
6. **Account-key access switch.** Left enabled during development; disable once managed identity is in use.

---

## 10. Implementation phases

1. **Options + provider switch.** Extend `StorageOptions`, add validation and DI selection; keep local default.
   Build and run the existing tests.
2. **`AzureBlobDocumentStorage`.** Implement against the SDK with lazy client, both auth modes, idempotent delete,
   safe error mapping. Add Azurite-backed integration tests (incl. extractor round-trip with the seekable stream).
3. **Ops wiring.** Health/startup container check, `appsettings.json` defaults, compose env passthrough,
   hook update for connection-string patterns.
4. **Docs.** `CLAUDE.md` (architecture + config table + backup note), `README.md` (Azure setup steps and the
   user-secrets commands above).
5. **Manual QA with the real storage account**, then (optionally) switch the account to Entra-only auth.

---

## 11. Definition of done
Provider is selectable by config; account name and container name are configuration; no secret is committed;
existing tests pass unchanged with `LocalDisk`; new Azurite integration tests pass; upload, processing, download
and delete work end-to-end against a real Azure container; docs updated.
