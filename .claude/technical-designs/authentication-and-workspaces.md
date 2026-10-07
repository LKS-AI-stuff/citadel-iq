# CitadelIQ — Technical Design: Authentication, Accounts and Workspaces

> Source: [`.claude/requirements/authentication-and-workspaces.md`](../requirements/authentication-and-workspaces.md).
> Builds on `design.md`, `postgresql-pgvector.md`, `rag.md` and `azure-blob-storage.md`, and on the project rules in
> `CLAUDE.md`. `design.md` §9/§21 deferred authentication and per-user spaces; this design is that deferred phase.

---

## 1. Objective

Add sign-in (Microsoft Entra External ID, OIDC), user accounts, and **workspaces** — Individual or Organization —
so that every folder, document, chunk and stored file belongs to exactly one workspace and no request can see or
change another workspace's data. Isolation is enforced twice: by the application (EF Core query filters and explicit
workspace ids) and by PostgreSQL row-level security (RLS). Organizations get onboarding by join code + approval,
Owner/Admin/Member roles, an admin page, and an "always at least one Owner" invariant that holds under concurrency.
The app keeps running in Docker with PostgreSQL + pgvector; only login is delegated to Entra.

---

## 2. Current state

| Area | What exists today | Where |
|---|---|---|
| Authentication | **None.** `app.UseAuthorization()` is in the pipeline but no scheme or policy is registered; every endpoint is anonymous. | `CitadelIQ.Api/Program.cs` |
| Single global space | `Folder.RootId = Guid.Empty` is the one "Home" folder, seeded by the first migration; `FoldersController.GetRootId` returns it; the UI hardcodes it as `ROOT_FOLDER_ID` and uses it for routing, the Back button and the search panel's default folder. `FolderService` rejects rename/delete by comparing to `Folder.RootId`. | `Domain/Entities/Folder.cs`, `FluentMigrations/Migrations/M202609300001_*.cs`, `Api/Controllers/FoldersController.cs`, `citadel-iq-ui/src/constants.ts`, `app/FolderPage.tsx`, `app/AppShell.tsx` |
| Lookups by id alone | `FolderRepository.GetByIdAsync`, `DocumentRepository.GetByIdAsync` load by primary key only. Download, status, delete, rename, folder contents and search's `CurrentFolderId` therefore accept **any** id from anyone. | `Infrastructure/Persistence/*Repository.cs`, `Application/Documents/DocumentService.cs`, `Application/Folders/FolderService.cs` |
| Whole-table queries | `FolderRepository.GetDescendantIdsAsync` loads **every** folder edge in the table, then walks the tree in memory. | `Infrastructure/Persistence/FolderRepository.cs` |
| Vector search | `SearchService.ResolveEligibleFolderIdsAsync` returns `null` for `EntirePortal`, and `VectorSearchRepository` then applies **no folder filter**: the search ranks every chunk in the database. Ask (`AnswerService.StartAsync`) reuses this, so Ask sources and LLM context would also be global. | `Application/Search/SearchService.cs`, `Infrastructure/Persistence/VectorSearchRepository.cs`, `Application/Rag/AnswerService.cs` |
| Background processing | `BackgroundDocumentProcessingDispatcher.Dispatch(documentId)` runs `ProcessDocumentAsync` in a new DI scope with no caller context. | `Infrastructure/Processing/BackgroundDocumentProcessingDispatcher.cs` |
| File storage | Files are stored as `{documentId}{ext}` at the root of `App_Data/documents` or the blob container. | `Infrastructure/Storage/*DocumentStorage.cs` |
| Rate limiting | Per **IP**, `/api/answers` only. | `Program.cs` |
| Database role | Docker and local dev connect as the container's `POSTGRES_USER`, which is a **superuser**. One connection string is used for both migrations and runtime. | `docker-compose.yml`, `README.md`, `FluentMigrations/DependencyInjection.cs` |
| Frontend ↔ API | Cross-origin. The UI calls `VITE_API_URL` (`:5157`) from `:5173`/`:8080`, with CORS `Cors:AllowedOrigins`. Downloads use `window.open(url)`, uploads use `XMLHttpRequest`, Ask uses `fetch` SSE. | `citadel-iq-ui/src/api/*.ts`, `Program.cs` |
| Errors | `ExceptionHandlingMiddleware` maps `NotFoundException`→404, `ValidationException`/`DomainException`→400, `FeatureDisabledException`→503, `AnswerGenerationException`→502. There is no 403 type. | `Api/Middleware/ExceptionHandlingMiddleware.cs` |
| Tests | `TestApp` runs the real services against a per-test database **as the container superuser**. `AnswersApiTests` uses `WebApplicationFactory<Program>`. | `CitadelIQ.Tests/Support/TestApp.cs`, `Integration/AnswersApiTests.cs` |
| Tooling | pgvector **0.8.6** in `pgvector/pgvector:pg17`, so iterative HNSW index scans are available. A `https` launch profile exists (`https://localhost:7274`). | local container, `Properties/launchSettings.json` |

---

## 3. Design overview

```
Browser (React, same origin)                     Entra External ID (hosted)
   │  cookie: __Host-citadeliq (HttpOnly)            ▲  OIDC auth-code + PKCE
   ▼                                                  │
nginx / Vite proxy ── /api, /auth, /signin-oidc ──► CitadelIQ.Api (BFF)
                                                     │ Cookie + OpenIdConnect handlers
                                                     │ CurrentUserMiddleware ─► ICurrentUser / IWorkspaceContext
                                                     │ Application services (role checks, invariants)
                                                     ▼
                                       EF Core ── WorkspaceConnectionInterceptor
                                                     │   set_config('app.workspace_id', …)
                                                     ▼
                     PostgreSQL (runtime role citadeliq_app, no BYPASSRLS)
                       Workspaces, Users, Memberships, JoinRequests   ← app-enforced
                       Folders, Documents, DocumentChunks             ← RLS: WorkspaceId = app.workspace_id
```

Every authenticated request runs these steps:

1. The cookie handler authenticates the user.
2. `CurrentUserMiddleware` loads the user and their membership from the database (one indexed query) and fills the scoped `CurrentUserContext`.
3. Every DB connection the request opens is stamped with that workspace id.
4. EF query filters add `WorkspaceId = @w` to every content query, and RLS enforces the same rule again inside Postgres.

---

## 4. Decisions made (with rationale)

1. **A workspace is the tenant.** An Individual account is a workspace with one member. An Organization account is a workspace with many members. Folders, documents, search and Ask use the same code for both; only membership, roles and the admin page differ.
2. **One workspace per user, enforced by the schema.** `Memberships` has `UserId` as its primary key.
   - The rule lives in a constraint, not scattered through the code.
   - Supporting several workspaces per user later means changing the key to `(UserId, WorkspaceId)` and adding a switcher. Nothing else in the model changes.
3. **The identity provider authenticates; CitadelIQ authorizes.**
   - Entra External ID handles sign-up, sign-in, email verification, password reset and MFA.
   - Users, memberships, roles and join requests live in CitadelIQ's database.
   - Users are keyed by **(issuer, subject)**, never by email.
   - The point: the identity provider stays swappable, role changes take effect immediately (no stale token claims), and org SSO can be added later without migrating data.
4. **Plain `Microsoft.AspNetCore.Authentication.OpenIdConnect` + Cookie, not `Microsoft.Identity.Web`.**
   - The API calls no downstream API with the user's token, so Identity.Web's token acquisition and caching would go unused.
   - The plain OIDC handler is provider-neutral: Entra, Keycloak or Auth0 differ only in configuration.
   - `SaveTokens = false`: no access or refresh tokens are stored anywhere. The ID token is validated once at sign-in, then discarded.
5. **Backend-for-frontend, same origin.**
   - The API runs the OIDC flow (authorization code + PKCE, confidential client) and issues an HttpOnly, Secure, SameSite=Lax cookie.
   - The SPA calls `/api/...` on its own origin through the Vite proxy (dev) or nginx (Docker). Requirement met: no token is ever readable by JavaScript.
   - Downloads (`window.open`), uploads (XHR) and SSE (`fetch`) keep working unchanged, because the browser sends the cookie on all of them.
   - CORS is no longer needed. `Cors:AllowedOrigins` defaults to empty.
6. **CSRF defense.**
   - SameSite=Lax already stops cookies being sent on cross-site POSTs.
   - Additionally, every state-changing `/api` request (anything but GET/HEAD) must carry the header `X-CSRF: 1`. A cross-origin page cannot add a custom header without a CORS preflight, and the API grants no cross-origin CORS. This is the same pattern Duende's BFF uses; it needs no tokens and is cheap.
   - All GET endpoints stay free of side effects.
7. **Session lifetime: the cookie handler, not the IdP session.**
   - Idle timeout: `ExpireTimeSpan` = `Authentication:Session:IdleTimeoutHours` (8) with sliding expiration.
   - Absolute lifetime: the issue time is stored in the ticket, and `OnValidatePrincipal` rejects tickets older than `AbsoluteLifetimeDays` (7).
   - Sign-out clears the cookie **and** redirects to Entra's end-session endpoint, so the next sign-in prompts again.
8. **Membership is re-read from the database on every request.**
   - Removal, closure and role changes take effect on the user's next request, as required.
   - Cost: one primary-key query (`Users` ⋈ `Memberships` by `(Issuer, Subject)`). Not cached, deliberately.
9. **Isolation is enforced twice: a workspace column + EF query filters + PostgreSQL RLS.**
   - Every content table (`Folders`, `Documents`, `DocumentChunks`) gets a `WorkspaceId`. It is copied down onto chunks so the vector query filters on the chunk row itself.
   - EF global query filters add the predicate to every LINQ query, including `ExecuteDeleteAsync`. RLS enforces the same rule in Postgres, even for code that bypasses EF.
   - Requests for another workspace's ids naturally become 404, because the row is invisible.
   - **The identity tables (`Workspaces`, `Users`, `Memberships`, `JoinRequests`) are not under RLS.** They must be readable before a workspace is known (sign-in, join-by-code). Only `AccountService` and `OrganizationAdminService` touch them, always with explicit ids.
10. **Composite foreign keys make a cross-workspace reference impossible.**
    - `Folders(WorkspaceId, ParentFolderId)`, `Documents(WorkspaceId, FolderId)` and `DocumentChunks(WorkspaceId, DocumentId)` each reference the parent's `(WorkspaceId, Id)`.
    - Even a bug that sets the wrong parent id cannot attach data to another workspace's tree.
11. **How the workspace reaches Postgres.**
    - A scoped EF `DbConnectionInterceptor` runs `set_config('app.workspace_id', <id or ''>, false)` every time EF opens a connection.
    - The value is always overwritten, including with an empty string when there is no workspace. A pooled connection therefore can never carry a stale value into another request (Npgsql's reset-on-return is a second safety net).
    - The policy reads `nullif(current_setting('app.workspace_id', true), '')::uuid`. With no workspace set, the result is NULL, which matches nothing: **fail closed**.
    - Cost: one extra round-trip per connection open, which is negligible next to an embedding call.
12. **A separate, restricted database role at runtime, checked at startup.**
    - Postgres superusers and roles with `BYPASSRLS` ignore RLS entirely, and today everything connects as a superuser.
    - Migrations therefore run as the owner (`ConnectionStrings:CitadelIQMigrations`). The API runs as `citadeliq_app` (`ConnectionStrings:CitadelIQ`): `LOGIN`, `NOSUPERUSER`, `NOBYPASSRLS`, with DML grants on the application tables only.
    - At startup the API runs `SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user` and **refuses to start** if the result is true, in every environment. Without this check, a misconfigured deployment would silently void the second layer.
13. **The vector search keeps recall under a tenant filter.**
    - HNSW collects `ef_search` candidates first and filters afterwards. A small workspace in a large table could get too few results, or none.
    - The interceptor also sets `hnsw.iterative_scan = strict_order` (pgvector ≥ 0.8; the image ships 0.8.6), so the index scan keeps going until `LIMIT` rows pass the filter.
    - A btree index on `DocumentChunks(WorkspaceId)` lets the planner choose an exact scan for small workspaces.
14. **Each workspace has its own root folder.**
    - It is the folder with `ParentFolderId IS NULL`; a partial unique index allows exactly one per workspace.
    - `Folder.RootId` (`Guid.Empty`) is **removed**. Keeping it as an obsolete constant would leave a misleading "well-known" id in the code.
    - The UI learns the root id from `GET /api/me`.
15. **Background processing carries the workspace explicitly.**
    - `IDocumentProcessingDispatcher.Dispatch(workspaceId, documentId)`.
    - The new scope enters that workspace before resolving `IDocumentService`, so RLS applies to background work too.
16. **Role checks live in the Application layer.**
    - Controllers only declare "authenticated + active member" (a fallback policy). Each service checks the role it needs (`currentUser.EnsureRole(WorkspaceRole.Admin)`), so the rules sit with the business logic and are covered by service-level tests.
    - The new `ForbiddenException` maps to 403. 403 is used only for "your own workspace, insufficient role"; another workspace's ids are always 404.
17. **Last-Owner invariant: a domain rule, applied under a lock.**
    - Every membership change (role change, removal, leave, approval) runs in one transaction that first runs `SELECT … FROM "Workspaces" WHERE "Id" = @w FOR UPDATE`, so all membership changes in a workspace are serialized.
    - Inside the transaction, the domain rule `MembershipRules.EnsureOwnerRemains` checks the resulting set.
    - Two Owners demoting each other at the same moment: the second transaction sees the first one's result and is rejected.
18. **Join codes are random, readable and stored in plain text.**
    - 12 characters of Crockford base32 from `RandomNumberGenerator` (60 bits), shown as `XXXX-XXXX-XXXX`, with a unique index.
    - Plain text (not hashed), because Admins must be able to see the code again on the admin page.
    - The risk is acceptable: guessing is rate-limited, and a correct guess only creates a request that an Admin still has to approve.
    - Regenerating replaces the code; pending requests made with the old code are kept.
19. **Join requests are kept as rows with a status** (`Pending`, `Approved`, `Rejected`, `Cancelled`), with a partial unique index allowing one `Pending` request per user. Keeping rejected rows lets onboarding say "your request to join *Acme* was declined" instead of silently resetting.
20. **Removing a member closes their account.**
    - The membership row is deleted, `Users.ClosedAtUtc` is set, and the user row is kept, so "uploaded by … (former member)" still resolves.
    - A closed identity always gets the "account closed" page. Coming back means signing up with a different email, which gives a new Entra subject and therefore a new account.
    - **Nothing is ever re-linked by email.** If someone comes back with the same email (for example after deleting and re-creating their Entra account), they still get a brand-new `Users` row. Their old uploads stay attributed to the old, closed account, shown as "former member". Email is not an identity: addresses get reassigned, and linking on it would let whoever controls an address inherit someone else's history.
21. **Requests already running when a member is removed are not cancelled.**
    - The next request is rejected, but an Ask stream that is already running finishes. It is bounded by `Rag:MaxOutputTokens` and typically lasts seconds.
    - Cancelling it would need a cross-request revocation registry, which is wrong as soon as there is more than one API instance.
    - This narrows the original requirement ("an open Ask stream is not continued"). **Accepted by the product owner.**
22. **Storage paths are prefixed with the workspace:** `{workspaceId}/{documentId}{ext}` for both blob names and local paths.
    - Files are still served only through the API's checks; this is not a per-tenant container.
    - The prefix makes per-workspace export, deletion and auditing possible later.
23. **The existing initial migration is rewritten in place; no new migration is added (confirmed: everything is development data).**
    - `M202609300001_InitialDocumentVectorSchema` is edited to create the final schema directly: identity tables, workspace columns, composite FKs, RLS, the runtime role and its grants. The `Guid.Empty` Home seed is dropped.
    - `M202610010001_AddSheetNameToDocumentChunks` is unchanged.
    - FluentMigrator's `VersionInfo` already records the old initial migration on existing databases, so it would **not** re-run there. Every existing dev database must therefore be reset: drop and recreate the database (or the Docker volume) and clear `App_Data/documents` or the blob container. The README's reset steps cover this, and the PR calls it out.
26. **`SearchScope.EntirePortal` is renamed to `SearchScope.EntireWorkspace`** in the enum, the API value, the frontend type and the UI label ("Entire workspace").
    - Meaning: the caller's whole workspace, from its root down. The user has access to all of it; there are no folder-level permissions.
    - It is a breaking API change, which is fine because the frontend ships with it and there are no other clients.
24. **Development login, Development environment only.**
    - `Authentication:Mode = DevelopmentLogin` replaces OIDC with a local form (`/auth/dev-login`: email + display name) that signs in a synthetic identity (issuer `urn:citadeliq:dev`).
    - Startup **throws** if this mode is configured outside the Development environment.
    - It lets the app run, and the UI be tested, without an Entra tenant or network access.
25. **Org SSO later.** Entra External ID can federate with an organization's own OIDC or SAML identity provider. With decision 3 in place, adding SSO means:
    - an `OrganizationSsoConnections` table plus verified domains, linked to an Entra federation per organization
    - sending sign-in to the right provider by email domain (`domain_hint`)
    - automatic provisioning into the organization instead of a join request

    The current model needs no change for this.

---

## 5. Proposed design by layer

### 5.1 Domain (`CitadelIQ.Domain`)

New enums:

- `WorkspaceKind { Individual, Organization }`
- `WorkspaceRole { Member = 0, Admin = 1, Owner = 2 }`. The values are ordered so that `role >= WorkspaceRole.Admin` reads naturally.
- `JoinRequestStatus { Pending, Approved, Rejected, Cancelled }`

New entities (factory methods + private setters, matching `Folder`/`Document`):

| Entity | Fields | Behaviour |
|---|---|---|
| `Workspace` | `Id`, `Kind`, `Name`, `JoinCode?`, `CreatedAtUtc` | `CreateIndividual(name)`, `CreateOrganization(name, joinCode)` (name: 2–100 chars, trimmed). `RegenerateJoinCode(code)` is allowed for organizations only. |
| `UserAccount` | `Id`, `Issuer`, `Subject`, `Email`, `DisplayName`, `CreatedAtUtc`, `LastSignInAtUtc`, `ClosedAtUtc?` | `Create(...)`, `RecordSignIn(email, displayName)` (refreshes the profile from claims), `Close()`, `IsClosed` |
| `Membership` | `UserId`, `WorkspaceId`, `Role`, `JoinedAtUtc` | `Create(...)`, `ChangeRole(role)` |
| `JoinRequest` | `Id`, `UserId`, `WorkspaceId`, `Status`, `CreatedAtUtc`, `DecidedAtUtc?`, `DecidedByUserId?` | `Approve(by)`, `Reject(by)`, `Cancel()`. Each is valid only while `Pending` and throws `DomainException` otherwise. |
| `MembershipRules` (static) | — | `EnsureOwnerRemains(IEnumerable<Membership>)` throws `DomainException("An organization must always have at least one Owner.")`. `CanManage(actorRole, targetCurrentRole, targetNewRole?)` encodes the permission table: Admins manage Members and Admins only; only Owners touch Owner. |
| `JoinCode` (static) | — | `Generate()` and `Normalize(input)`. `Normalize` uppercases, strips `-` and whitespace, and maps the ambiguous `I/L→1` and `O→0` as Crockford base32 does. |

Changed entities:

- **`Folder`**
  - Adds `WorkspaceId`.
  - `CreateRoot(workspaceId)` replaces the parameterless `CreateRoot()`.
  - `CreateChild(Folder parent, string name)` replaces `Create(name, parentFolderId)`. The child copies the parent's `WorkspaceId`, so a caller cannot get it wrong.
  - Adds `IsRoot => ParentFolderId is null`.
  - The `RootId` constant is **removed**.
- **`Document`**
  - Adds `WorkspaceId` and `UploadedByUserId` (nullable in the schema for safety; always set by upload).
  - `Create(Folder folder, Guid uploadedByUserId, fileName, contentType, sizeBytes)` takes the workspace from the folder.
- **`DocumentChunk`**
  - Adds `WorkspaceId`.
  - `Create(Document document, …)` copies it from the document.
- `DocumentEmbedding` is unchanged; the embedding is a column on the chunk row.

### 5.2 Application (`CitadelIQ.Application`)

**Current user and workspace context** (`Accounts/`):

```csharp
public interface ICurrentUser                 // read by services
{
    Guid? UserId { get; }
    Guid? WorkspaceId { get; }
    WorkspaceKind? WorkspaceKind { get; }
    WorkspaceRole? Role { get; }
    bool IsActiveMember { get; }              // has a membership and is not closed
    Guid RequireUserId();                     // throws UnauthenticatedException
    void EnsureRole(WorkspaceRole minimum, string action);   // throws ForbiddenException("You don't have permission to {action}.")
}

public interface IWorkspaceContext            // read by Infrastructure (query filters, RLS interceptor)
{
    Guid? WorkspaceId { get; }
    void Enter(Guid workspaceId);             // once per scope; a second, different id throws InvalidOperationException
}
```

- Both are implemented by one scoped class, `CurrentUserContext`, with `SetUser(UserAccount, Membership?, Workspace?)`.
- Only the Api middleware, the dispatcher and the onboarding service set it.
- It contains no ASP.NET types, so Clean Architecture holds.

**Ports** (`Interfaces/`):

- `IUserAccountRepository`:
  - `GetByIdentityAsync(issuer, subject)`
  - `GetByIdAsync`
  - `GetDisplayInfoAsync(IReadOnlyCollection<Guid> ids)`, which returns id → (display name, closed?) for uploader display
  - `AddAsync`, `UpdateAsync`
- `IWorkspaceRepository`:
  - `GetByIdAsync`
  - `GetByJoinCodeAsync(normalizedCode)`
  - `AddAsync`, `UpdateAsync`
- `IMembershipRepository`:
  - `GetByUserIdAsync`
  - `ListByWorkspaceAsync` (joined with user display info)
  - `RunLockedAsync(Guid workspaceId, Func<WorkspaceMembershipSet, Task> change)`. It opens a transaction, locks the `Workspaces` row (`FOR UPDATE`), loads the workspace's memberships and pending join requests into a `WorkspaceMembershipSet`, runs `change`, saves, and commits.
  - `RunLockedAsync` is the **only** way to change memberships, which is what serializes them.
- `IJoinRequestRepository`:
  - `GetPendingForUserAsync`, `GetLatestForUserAsync`, `ListPendingAsync(workspaceId)`
  - `AddAsync` (translates the unique-violation on "one pending per user" into a `ValidationException`, as `FolderRepository` does for folder names)
- `IUnitOfWork.ExecuteInTransactionAsync(Func<Task>)`. Used by onboarding to create workspace + root folder + membership atomically. The EF implementation uses `Database.CreateExecutionStrategy` + `BeginTransactionAsync`.
- `IFolderRepository`:
  - Adds `GetRootAsync()`.
  - `GetDescendantIdsAsync` stays as it is. It now loads only the caller's workspace edges, thanks to the filter, which also fixes the whole-table scan.
- `IDocumentStorage`: every method gains a `Guid workspaceId` parameter (decision 22).
- `IDocumentProcessingDispatcher.Dispatch(Guid workspaceId, Guid documentId)`.
- `IVectorSearchRepository` is unchanged. `DocumentSearchResult` gains `Guid? UploadedByUserId`. Workspace scoping comes from the filter and RLS, and `null` folder ids now means "the whole workspace".

**Services:**

- **`AccountService`** (`Accounts/`):
  - `EnsureUserAsync(issuer, subject, email, displayName)`:
    - called from the OIDC `OnTokenValidated` event
    - creates the user on first sign-in; refreshes email and name otherwise
  - `ResolveAsync(issuer, subject)`:
    - called by the middleware
    - returns `(UserAccount?, Membership?, Workspace?)`
  - `GetSessionAsync()`: builds `SessionDto` for `GET /api/me`. Status rules:
    - `Closed` if the user is closed
    - otherwise `Active` if they have a membership
    - otherwise `PendingApproval` if they have a pending request
    - otherwise `NeedsOnboarding`, including the latest rejected request's organization name, if any
- **`OnboardingService`** (`Accounts/`). Every method requires status `NeedsOnboarding`; anything else is a `ValidationException`.
  - `CreateIndividualAsync()`:
    - workspace name = display name
    - in one transaction: Workspace, root `Folder.CreateRoot`, Owner membership
    - **Order matters:** create the workspace id → `workspaceContext.Enter(id)` → begin the transaction. The interceptor stamps the connection when the transaction opens it, so the root-folder insert passes RLS's `WITH CHECK`.
  - `CreateOrganizationAsync(name)`: the same, with a generated join code. The creator becomes Owner.
  - `RequestToJoinAsync(code)`:
    - normalize the code and look it up
    - unknown code, or a code belonging to an Individual workspace: `ValidationException("That code wasn't recognised.")`, the same generic message in both cases
    - otherwise add a `Pending` request
  - `CancelJoinRequestAsync()`.
- **`OrganizationAdminService`** (`Organizations/`). Requires `Kind == Organization` (otherwise `NotFoundException`) and the role noted for each method:
  - `ListMembersAsync`, `ListJoinRequestsAsync`, `GetJoinCodeAsync`, `RegenerateJoinCodeAsync`: Admin+.
  - `ChangeRoleAsync(userId, role)`, `RemoveMemberAsync(userId)`, `ApproveAsync(requestId, role)`, `RejectAsync(requestId)`: Admin+. Each runs inside `RunLockedAsync`: `MembershipRules.CanManage` → apply the change → `EnsureOwnerRemains` → save.
  - `RemoveMemberAsync` also closes the user's account (decision 20).
  - Approving as Owner requires the actor to be an Owner.
  - `LeaveAsync()`: any member of an organization. It is removal of self: same lock, same invariant, and the account is closed. Not available for Individual workspaces.
- **`FolderService`**:
  - Every `== Folder.RootId` becomes `folder.IsRoot`.
  - `CreateFolderAsync` uses `Folder.CreateChild(parent, name)`.
  - `RenameFolderAsync` and `DeleteFolderAsync` call `currentUser.EnsureRole(Admin, "rename folders" / "delete folders")`.
  - New `GetRootAsync()` returns the caller's root id.
- **`DocumentService`**:
  - `UploadDocumentAsync`: `Document.Create(folder, currentUser.RequireUserId(), …)`; storage and dispatch now pass `document.WorkspaceId`.
  - `DeleteDocumentAsync`: `EnsureRole(Admin, "delete documents")`.
  - `DeleteDocumentsAsync` stays internal: no role check, because folder delete has already checked.
  - `ProcessDocumentAsync` creates chunks with `DocumentChunk.Create(document, …)`.
  - The summary DTO gets uploader info through a batch `GetDisplayInfoAsync`.
- **`SearchService`**:
  - `SearchScope.EntirePortal` is renamed `EntireWorkspace` (decision 26). It still returns `null` folder ids, which the query filter and RLS confine to the caller's workspace.
  - Results get `UploadedBy` through a batch lookup cached per search, mirroring the folder-path cache.
  - `AnswerService` and `ContextSelector` pass it through into `AnswerSourceDto`.

**DTOs:**

- `SessionDto`:
  - `Status` (`NeedsOnboarding|PendingApproval|Active|Closed`)
  - `User { Id, DisplayName, Email }`
  - `Workspace? { Id, Name, Kind, RootFolderId }`
  - `Role?`
  - `PendingJoinRequest? { OrganizationName, CreatedAtUtc }`
  - `LastRejectedOrganizationName?`
- `MemberDto { UserId, DisplayName, Email, Role, JoinedAtUtc, IsCurrentUser }`
- `JoinRequestDto { Id, DisplayName, Email, CreatedAtUtc }`
- `JoinCodeDto { Code }` (formatted as `XXXX-XXXX-XXXX`)
- `UploaderDto { DisplayName, IsFormerMember }`, added as `UploadedBy?` to `DocumentSummaryDto`, `SearchResultDto` and `AnswerSourceDto`. It is null when the uploader is unknown.

**Exceptions** (`Common/`): `ForbiddenException` (403) and `UnauthenticatedException` (401), mapped by the middleware.

**Options** (`Options/AuthenticationOptions.cs`, bound from `Authentication`, validated on start like `RagOptions`):

- `Mode` (`Oidc` | `DevelopmentLogin`)
- `Oidc { Authority, ClientId, ClientSecret, Scopes, SignedOutRedirectPath }`
- `Session { CookieName, IdleTimeoutHours, AbsoluteLifetimeDays }`
- `JoinRateLimit { PermitLimit, WindowMinutes }`

`ClientSecret` is a secret: user-secrets or an environment variable only, never in `appsettings.json`. The existing `block-appsettings-secrets.py` hook should be extended to cover it.

### 5.3 Infrastructure (`CitadelIQ.Infrastructure`)

- **`CitadelIQDbContext`**:
  - Injects `IWorkspaceContext`.
  - Adds `DbSet`s for `Workspaces`, `Users`, `Memberships`, `JoinRequests`, plus configurations for each.
  - Global query filters: `HasQueryFilter(x => x.WorkspaceId == workspaceContext.WorkspaceId)` on `Folder`, `Document` and `DocumentChunk`. EF parameterizes the context field per query. When it is null, the filter matches nothing.
  - `IgnoreQueryFilters()` is **not used anywhere** (a grep-able rule).
- **`WorkspaceConnectionInterceptor`** (scoped `DbConnectionInterceptor`):
  - `ConnectionOpenedAsync` (and the sync variant) runs
    `SELECT set_config('app.workspace_id', @w, false), set_config('hnsw.iterative_scan', 'strict_order', false);`
    with `@w` = the workspace id or `''`.
  - Registered via `AddDbContext((sp, o) => o.UseNpgsql(...).AddInterceptors(sp.GetRequiredService<WorkspaceConnectionInterceptor>()))`.
- **Repositories:**
  - New: `UserAccountRepository`, `WorkspaceRepository`, `MembershipRepository` (implements `RunLockedAsync` with `FromSqlInterpolated($"SELECT * FROM \"Workspaces\" WHERE \"Id\" = {id} FOR UPDATE")` inside a transaction), `JoinRequestRepository`, `EfUnitOfWork`.
  - Existing repositories need **no code change** for scoping; the filter and RLS do it. `VectorSearchRepository` additionally projects `UploadedByUserId`.
- **`DatabaseRoleGuard`** (`IHostedService`, or a startup check called from `Program`): runs the superuser/`BYPASSRLS` query from decision 12 and throws if it is true.
- **Storage:**
  - `LocalDiskDocumentStorage` path: `{DocumentsPath}/{workspaceId}/{documentId}{ext}`, creating the directory on save.
  - `AzureBlobDocumentStorage.BlobName`: `{workspaceId}/{documentId}{ext}`.
- **`BackgroundDocumentProcessingDispatcher.Dispatch(workspaceId, documentId)`**: inside the new scope, `scope.ServiceProvider.GetRequiredService<IWorkspaceContext>().Enter(workspaceId)` runs **before** `IDocumentService` is resolved and used.

### 5.4 Api (`CitadelIQ.Api`)

**Authentication registration** (`Authentication/AuthenticationSetup.cs`, called from `Program.cs`):

- Cookie scheme (default):
  - `Cookie.Name` (`__Host-citadeliq` by default), `HttpOnly`, `SecurePolicy = Always`, `SameSite = Lax`, `Path = /`, sliding `ExpireTimeSpan`.
  - `Events.OnRedirectToLogin` / `OnRedirectToAccessDenied` → 401/403 ProblemDetails for `/api/*` (never a 302 to an HTML page).
  - `OnValidatePrincipal` enforces the absolute lifetime.
- OpenIdConnect scheme (`Mode = Oidc`, default challenge):
  - `Authority`, `ClientId`, `ClientSecret`, `ResponseType = code`, `UsePkce = true`, `SaveTokens = false`, scopes `openid profile email`, `MapInboundClaims = false`.
  - `OnTokenValidated` calls `AccountService.EnsureUserAsync(iss, sub, email, name)` and replaces the principal with a minimal one: `iss`, `sub`, `name`. No roles go into the cookie (decision 8).
  - `OnRemoteFailure` redirects to `/?signin=failed`, so the UI can show a friendly "sign-in failed" message and the IdP error is never shown.
- `DevelopmentLogin` mode: a minimal controller (`/auth/dev-login` GET form, POST to sign in) that is mapped only when the mode is set and the environment is Development. Otherwise startup throws.
- Fallback authorization policy: authenticated **and** `ICurrentUser.IsActiveMember` (custom `ActiveMemberRequirement`).
  - `[AllowAnonymous]`: `/health`, `/health/storage`, `/api/settings`, `/auth/*`.
  - `[Authorize(Policy = "Authenticated")]` (signed in, membership not required): `/api/me` and `/api/onboarding/*`.

**Middleware order in `Program.cs`:**

```
UseForwardedHeaders (+ XForwardedHost, needed for the OIDC redirect_uri behind nginx)
→ ExceptionHandlingMiddleware
→ UseAuthentication
→ CurrentUserMiddleware        // resolves iss/sub → user, membership, workspace; sets CurrentUserContext; closed → 403 "account closed" for /api/* except /api/me
→ CsrfHeaderMiddleware         // non-GET/HEAD /api/* without X-CSRF: 1 → 400; POST /auth/logout needs X-CSRF or a same-origin Origin header (it is a plain form post)
→ UseRateLimiter               // partitions by user id when known, else IP
→ UseAuthorization
→ MapControllers
```

`UseCors` stays registered but with an empty origin list by default (same origin). It is still available if someone deliberately deploys cross-origin.

**Controllers:**

- New `AuthController` (`/auth`):
  - `GET /auth/login?returnUrl=` issues the challenge. `returnUrl` must be a local path (`Url.IsLocalUrl`).
  - `POST /auth/logout` signs out of the cookie and OIDC schemes.
- New `MeController` (`GET /api/me`).
- New `OnboardingController` (`/api/onboarding`).
- New `OrganizationController` (`/api/organization`): members, join requests, join code, leave.
- `FoldersController.GetRootId` returns `folderService.GetRootAsync()`. It no longer references `Folder.RootId`.
- Other controllers are unchanged; scoping and roles live in the services and below.

**`ExceptionHandlingMiddleware`:** add `ForbiddenException` → 403 and `UnauthenticatedException` → 401, both using the exception's safe message.

**Rate limiting:**

- The existing `/api/answers` limiters use the user id as partition key (falling back to IP).
- New fixed-window limiter on `POST /api/onboarding/join-requests`, per user: `Authentication:JoinRateLimit` 10 per 60 minutes.

### 5.5 Frontend (`citadel-iq-ui`)

- **Same origin:**
  - `vite.config.ts` gets `server.proxy` entries for `/api`, `/auth`, `/signin-oidc` and `/signout-callback-oidc` → `https://localhost:7274`. It uses `changeOrigin: false`, so the redirect URI keeps the `localhost:5173` host, and `secure: false` for the dev certificate.
  - `API_BASE_URL` becomes `''` (relative paths), and `VITE_API_URL` is removed.
- **`apiClient.ts` / `sse.ts` / `documentsApi.ts`:**
  - Add `X-CSRF: 1` to every non-GET request, including the XHR upload and the SSE POST.
  - On a 401 from any call, notify `SessionProvider`, which redirects to `/auth/login?returnUrl=<current path>`.
  - On 403 with the "account closed" title, refetch the session.
- **`session/SessionProvider`** + `useSession()`: fetches `GET /api/me` once at load and on demand (`refresh()`). It exposes `status`, `user`, `workspace`, `role`, `can.deleteContent`, `can.renameFolders` and `can.administer`. It is mounted in `main.tsx` above the router, next to `SettingsProvider`.
- **Gating** (`app/SessionGate.tsx`, wrapping `AppShell`'s routes):

  | Session state | Page shown |
  |---|---|
  | Signed out (401) | `SignInPage`: the product pitch plus a "Sign in / Create account" button that links to `/auth/login`. Entra's combined sign-up/sign-in flow handles both. |
  | `NeedsOnboarding` | `OnboardingPage`: three `GlassSurface` cards (Individual / Create organization with a name field / Join organization with a code field). Shows "your request to join X was declined" when relevant. |
  | `PendingApproval` | `PendingApprovalPage`: "Waiting for an admin of *X* to approve", with Cancel request and Sign out. |
  | `Closed` | `AccountClosedPage`: explanation plus Sign out. |
  | `Active` | The current app. |

  All of these reuse the glass styling, `FloatingShapes` and `Footer`, without the sidebar.
- **Root folder:**
  - Remove `constants.ts`'s `ROOT_FOLDER_ID`.
  - `FolderPage`, `AppShell` and the Back button use `session.workspace.rootFolderId`.
  - The `/` route still means "root"; `/folders/:id` is unchanged.
- **`TopNavigation`** gets an account menu (`IconButton` → MUI `Menu`): display name and email, workspace name with a Personal/Organization chip, an "Organization admin" link (Admin+ in an organization), and "Leave organization" (organization members, behind a `ConfirmDialog` that warns the account will be closed). Sign out is a `<form method="post" action="/auth/logout">`: a form POST cannot set `X-CSRF`, so `/auth/logout` instead accepts either the header or a same-origin `Origin` header.
- **Role-aware cards:**
  - `FileCard` hides Delete unless `can.deleteContent`.
  - `FolderCard` hides Rename and Delete unless `can.renameFolders` / `can.deleteContent`.
  - The server enforces the same rules regardless.
- **Uploader:**
  - `FileCard` and `SearchResultCard` (and therefore the Ask `SourceList`) show "Uploaded by *Name*", or "*Name* (former member)", in `text.secondary` caption type.
  - Shown only when `workspace.kind === 'Organization'`.
- **`AdminPage`** (route `/admin`, Admin+ in an organization; anyone else is redirected to `/`). One `GlassSurface` panel with three `SectionHeader` sections:
  1. **Join requests:** name, email, requested date; Approve (with a role `Select`, default Member; Owner is offered only to Owners) and Reject.
  2. **Members:** table of name, email, role `Select`, joined date and a Remove `IconButton` (error color). The client applies the same rules as the server:
     - an Admin sees Owner rows read-only
     - the last Owner's role select and Remove button are disabled, with a tooltip "An organization needs at least one Owner"
     - the server's 400 message is shown as a toast if a race still hits the rule
  3. **Join code:** the code in monospace, Copy and Regenerate (Regenerate behind a `ConfirmDialog`).

  Hooks: `useMembers`, `useJoinRequests`, `useJoinCode`, in the style of the existing `use*` hooks. API module: `organizationApi.ts`. Types: `types/session.ts`, `types/organization.ts`.
- `types/search.ts` renames `EntirePortal` → `EntireWorkspace`, and `SearchScopeSelector` labels it "Entire workspace" (decision 26).

---

## 6. Data model changes

No new migration. `M202609300001_InitialDocumentVectorSchema` is rewritten to produce this schema, and existing dev databases are reset (decision 23). It runs as the owner role.

For readability, the SQL below is written as changes to today's schema. In the rewritten migration, the new columns and constraints are folded straight into the `Create.Table` calls (raw `Execute.Sql` for the composite FKs, partial indexes, RLS and role), and nothing is deleted.

```sql
-- 1. Identity tables (no RLS).
CREATE TABLE "Workspaces" (
  "Id" uuid PRIMARY KEY, "Kind" varchar(16) NOT NULL, "Name" varchar(100) NOT NULL,
  "JoinCode" varchar(12) NULL, "CreatedAtUtc" timestamptz NOT NULL,
  CONSTRAINT "CK_Workspaces_JoinCode" CHECK (("Kind" = 'Organization') = ("JoinCode" IS NOT NULL)));
CREATE UNIQUE INDEX "UX_Workspaces_JoinCode" ON "Workspaces" ("JoinCode") WHERE "JoinCode" IS NOT NULL;

CREATE TABLE "Users" (
  "Id" uuid PRIMARY KEY, "Issuer" varchar(512) NOT NULL, "Subject" varchar(255) NOT NULL,
  "Email" varchar(320) NOT NULL, "DisplayName" varchar(200) NOT NULL,
  "CreatedAtUtc" timestamptz NOT NULL, "LastSignInAtUtc" timestamptz NOT NULL, "ClosedAtUtc" timestamptz NULL);
CREATE UNIQUE INDEX "UX_Users_Issuer_Subject" ON "Users" ("Issuer", "Subject");

CREATE TABLE "Memberships" (
  "UserId" uuid PRIMARY KEY REFERENCES "Users"("Id"),              -- one workspace per user
  "WorkspaceId" uuid NOT NULL REFERENCES "Workspaces"("Id") ON DELETE CASCADE,
  "Role" varchar(16) NOT NULL, "JoinedAtUtc" timestamptz NOT NULL);
CREATE INDEX "IX_Memberships_WorkspaceId" ON "Memberships" ("WorkspaceId");

CREATE TABLE "JoinRequests" (
  "Id" uuid PRIMARY KEY, "UserId" uuid NOT NULL REFERENCES "Users"("Id"),
  "WorkspaceId" uuid NOT NULL REFERENCES "Workspaces"("Id") ON DELETE CASCADE,
  "Status" varchar(16) NOT NULL, "CreatedAtUtc" timestamptz NOT NULL,
  "DecidedAtUtc" timestamptz NULL, "DecidedByUserId" uuid NULL REFERENCES "Users"("Id"));
CREATE UNIQUE INDEX "UX_JoinRequests_PendingPerUser" ON "JoinRequests" ("UserId") WHERE "Status" = 'Pending';
CREATE INDEX "IX_JoinRequests_WorkspaceId_Status" ON "JoinRequests" ("WorkspaceId", "Status");

-- 2. Workspace columns + composite keys on content tables.
ALTER TABLE "Folders" ADD "WorkspaceId" uuid NOT NULL REFERENCES "Workspaces"("Id") ON DELETE CASCADE;
ALTER TABLE "Folders" ADD CONSTRAINT "UQ_Folders_WorkspaceId_Id" UNIQUE ("WorkspaceId", "Id");
ALTER TABLE "Folders" DROP CONSTRAINT "FK_Folders_Folders_ParentFolderId";
ALTER TABLE "Folders" ADD CONSTRAINT "FK_Folders_Parent_SameWorkspace"
  FOREIGN KEY ("WorkspaceId", "ParentFolderId") REFERENCES "Folders" ("WorkspaceId", "Id") ON DELETE CASCADE;  -- MATCH SIMPLE: root's NULL parent is unchecked
CREATE UNIQUE INDEX "UX_Folders_WorkspaceRoot" ON "Folders" ("WorkspaceId") WHERE "ParentFolderId" IS NULL;
-- existing UX_Folders_ParentFolderId_LowerName stays (parent ids are workspace-specific)

ALTER TABLE "Documents" ADD "WorkspaceId" uuid NOT NULL;
ALTER TABLE "Documents" ADD "UploadedByUserId" uuid NULL REFERENCES "Users"("Id");
ALTER TABLE "Documents" ADD CONSTRAINT "UQ_Documents_WorkspaceId_Id" UNIQUE ("WorkspaceId", "Id");
ALTER TABLE "Documents" DROP CONSTRAINT "FK_Documents_Folders_FolderId";
ALTER TABLE "Documents" ADD CONSTRAINT "FK_Documents_Folder_SameWorkspace"
  FOREIGN KEY ("WorkspaceId", "FolderId") REFERENCES "Folders" ("WorkspaceId", "Id") ON DELETE CASCADE;

ALTER TABLE "DocumentChunks" ADD "WorkspaceId" uuid NOT NULL;
ALTER TABLE "DocumentChunks" DROP CONSTRAINT "FK_DocumentChunks_Documents_DocumentId";
ALTER TABLE "DocumentChunks" ADD CONSTRAINT "FK_DocumentChunks_Document_SameWorkspace"
  FOREIGN KEY ("WorkspaceId", "DocumentId") REFERENCES "Documents" ("WorkspaceId", "Id") ON DELETE CASCADE;
CREATE INDEX "IX_DocumentChunks_WorkspaceId" ON "DocumentChunks" ("WorkspaceId");

-- 3. Row-level security (fail closed when app.workspace_id is unset/empty).
-- for each of "Folders", "Documents", "DocumentChunks":
ALTER TABLE "Folders" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "Folders" FORCE ROW LEVEL SECURITY;
CREATE POLICY "Folders_workspace_isolation" ON "Folders"
  USING      ("WorkspaceId" = nullif(current_setting('app.workspace_id', true), '')::uuid)
  WITH CHECK ("WorkspaceId" = nullif(current_setting('app.workspace_id', true), '')::uuid);

-- 4. Runtime role and grants (role is created NOLOGIN if absent; the operator/compose init script gives it LOGIN + password).
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'citadeliq_app') THEN
    CREATE ROLE citadeliq_app NOLOGIN NOSUPERUSER NOBYPASSRLS;
  END IF;
END $$;
GRANT USAGE ON SCHEMA public TO citadeliq_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON "Folders", "Documents", "DocumentChunks",
  "Workspaces", "Users", "Memberships", "JoinRequests" TO citadeliq_app;
-- no grant on "VersionInfo"; no DDL rights
```

Notes:

- **Foreign-key cascades and checks are not filtered by RLS.** Deleting a folder still removes its subtree and documents. Because the keys are composite, a cascade can never cross into another workspace anyway.
- **Hand-mirror the migration in the EF configurations** (`CLAUDE.md` rule):
  - the `WorkspaceId` properties and composite FKs in `FolderConfiguration`, `DocumentConfiguration` and `DocumentChunkConfiguration`
  - new `WorkspaceConfiguration`, `UserAccountConfiguration`, `MembershipConfiguration`, `JoinRequestConfiguration`
  - enums stored as strings, like `ProcessingStatus`
- **New convention for `CLAUDE.md` ("Adding a schema change"):**
  - every new table must be granted to `citadeliq_app`
  - every new table holding workspace content needs `WorkspaceId`, a composite FK, an RLS policy and an EF query filter

---

## 7. API contract changes

New endpoints:

```
GET    /auth/login?returnUrl=/path       # 302 to Entra (challenge); anonymous
POST   /auth/logout                      # clears cookie, 302 to Entra end-session then back to "/"
GET    /signin-oidc, /signout-callback-oidc   # handled by the OIDC middleware

GET    /api/me                           # SessionDto (401 if not signed in)
POST   /api/onboarding/individual        # → SessionDto (Active)
POST   /api/onboarding/organization      # { name } → SessionDto (Active, Owner)
POST   /api/onboarding/join-requests     # { joinCode } → SessionDto (PendingApproval); 400 "That code wasn't recognised."; 429
DELETE /api/onboarding/join-requests/current   # cancel → SessionDto (NeedsOnboarding)

GET    /api/organization/members                         # Admin+  → MemberDto[]
PUT    /api/organization/members/{userId}/role           # Admin+  { role } → MemberDto; 400 last-Owner / not allowed
DELETE /api/organization/members/{userId}                # Admin+  → 204 (closes that account); 400 last-Owner
POST   /api/organization/leave                           # any org member → 204 (closes own account); 400 last-Owner
GET    /api/organization/join-requests                   # Admin+  → JoinRequestDto[] (pending)
POST   /api/organization/join-requests/{id}/approve      # Admin+  { role } → MemberDto
POST   /api/organization/join-requests/{id}/reject       # Admin+  → 204
GET    /api/organization/join-code                       # Admin+  → { code }
POST   /api/organization/join-code/regenerate            # Admin+  → { code }
```

Changed behaviour of the existing endpoints:

| Endpoint | Change |
|---|---|
| All `/api/*` except `/api/settings` | 401 if signed out. 403 "Your account has been closed." if closed. 403 if onboarding is not complete (except `/api/me` and `/api/onboarding/*`). |
| `GET /api/folders/root` | Returns the caller's workspace root id (no longer `Guid.Empty`). |
| Any `{folderId}` / `{documentId}` from another workspace | 404 with the same message as a missing id. |
| `PUT /api/folders/{id}`, `DELETE /api/folders/{id}`, `DELETE /api/documents/{id}` | 403 for Members. |
| `POST /api/search`, `POST /api/answers/stream` | `searchScope: "EntirePortal"` is renamed `"EntireWorkspace"` (the old value is rejected with 400), meaning the caller's whole workspace. Results/sources gain `uploadedBy: { displayName, isFormerMember } \| null`. |
| `GET /api/folders/{id}/contents`, `POST /api/documents/upload` | `documents[]` / the response gain `uploadedBy`. |
| All non-GET `/api/*` | Require the header `X-CSRF: 1` (400 without it). |
| `/api/answers/*` rate limits | Per user instead of per IP. |

`ProcessingStatus`/`SearchScope` serialization is unchanged. The new enums (`WorkspaceKind`, `WorkspaceRole`, session status) serialize as PascalCase strings through the existing `JsonStringEnumConverter`.

---

## 8. Configuration reference

| Section | Key | Default | Notes |
|---|---|---|---|
| `ConnectionStrings` | `CitadelIQ` | *(empty)* | **Runtime**, as `citadeliq_app`. Startup refuses a superuser or `BYPASSRLS` role. |
| `ConnectionStrings` | `CitadelIQMigrations` | *(empty)* | **Owner** role, used only for migrations. Required when `Database:MigrateOnStartup` is true. |
| `Authentication` | `Mode` | `Oidc` | `DevelopmentLogin` is allowed only in the Development environment. |
| `Authentication:Oidc` | `Authority` | `""` | e.g. `https://<subdomain>.ciamlogin.com/<tenant-id>/v2.0` (exact format confirmed in Phase 0). |
| `Authentication:Oidc` | `ClientId` | `""` | App registration id. |
| `Authentication:Oidc` | `ClientSecret` | *(secret)* | user-secrets / `Authentication__Oidc__ClientSecret` only. |
| `Authentication:Oidc` | `Scopes` | `["openid","profile","email"]` | |
| `Authentication:Session` | `CookieName` | `__Host-citadeliq` | The `__Host-` prefix requires HTTPS, which is why dev uses the `https` profile. |
| `Authentication:Session` | `IdleTimeoutHours` / `AbsoluteLifetimeDays` | `8` / `7` | |
| `Authentication:JoinRateLimit` | `PermitLimit` / `WindowMinutes` | `10` / `60` | Join-code attempts per user. |
| `Cors` | `AllowedOrigins` | `[]` | Same origin; only needed for a deliberate cross-origin deployment. |

---

## 9. Non-functional considerations

- **Security:**
  - **Two independent layers.** Leaking data needs both an application bug (a missing filter, a wrong id) and an RLS gap.
  - **Fail closed.** No workspace means no rows.
  - **No tokens in the browser.** Cookie only, HttpOnly + Secure + SameSite=Lax, with the CSRF header on top.
  - **Open-redirect protection.** `returnUrl` must be a local path.
  - **No existence leak.** Another workspace's ids return 404. An unknown join code and an Individual workspace's code get the same message.
  - **No IdP errors shown.** IdP failures and claims never reach the UI.
- **Logging:**
  - Add `UserId` and `WorkspaceId` to the logging scope in `CurrentUserMiddleware`.
  - Never log email addresses, join codes or tokens, and still never log document content, questions or answers (RAG rule).
  - Membership changes are logged as `"{ActorId} changed {TargetId} to {Role}"` (ids only). No history table (confirmed).
- **Performance:**
  - Per request: one extra indexed query (resolving the user and membership).
  - Per connection open: one `set_config` round-trip.
  - The `GetDescendantIdsAsync` whole-table scan becomes a per-workspace scan.
  - Vector search: iterative scan plus a `WorkspaceId` btree. Check `EXPLAIN ANALYZE` in Phase 2 with two workspaces of very different sizes.
- **Clean Architecture:**
  - `ICurrentUser`/`IWorkspaceContext` and the rules are Application/Domain.
  - The OIDC, cookie, CSRF and middleware code is Api.
  - The interceptor, RLS and role guard are Infrastructure.
  - Controllers stay thin.
- **Async and cancellation:** all new I/O is async. `RunLockedAsync` uses `CancellationToken.None` for the commit once the change has been applied, so a client disconnect cannot leave a half-applied change.

---

## 10. Deployment notes

- **Entra External ID setup** (README, done once; details confirmed in Phase 0):
  1. Create an external tenant.
  2. Create a "Sign up and sign in" user flow: email + password, email verified by one-time code, collecting display name.
  3. Register the app (web platform).
  4. Add redirect URIs `https://localhost:5173/signin-oidc` (dev) and `https://<host>/signin-oidc`, plus the matching `/signout-callback-oidc`.
  5. Create a client secret.
  6. Add `email` as an optional ID-token claim.
- **docker-compose:**
  - The `db` service gets `deploy/postgres/initdb/01-app-role.sh`, run at first volume initialization. It creates `citadeliq_app LOGIN PASSWORD '${APP_DB_PASSWORD}' NOSUPERUSER NOBYPASSRLS`.
  - The API receives both connection strings, `Authentication__Oidc__*` and `ForwardedHeaders__KnownProxies__0` (the nginx container).
  - `ui` nginx proxies `/api`, `/auth`, `/signin-oidc` and `/signout-callback-oidc` to `api:8080`:
    - `proxy_buffering off` for `/api/answers/stream`
    - `client_max_body_size 50m`
    - forwarded headers (`X-Forwarded-Proto/Host/For`)
  - The API port no longer needs to be published.
  - `PUBLIC_API_URL`/`VITE_API_URL` are removed. New `.env` keys: `APP_DB_PASSWORD`, `OIDC_AUTHORITY`, `OIDC_CLIENT_ID`, `OIDC_CLIENT_SECRET`.
  - **TLS is required.** `Secure` cookies and Entra redirect URIs need HTTPS for a non-localhost host, so a real deployment puts TLS in front of nginx (or on nginx). The current "don't expose it publicly" note in `CLAUDE.md` is replaced by "expose only over HTTPS".
- **Local dev:**
  - The README adds the commands to create `citadeliq_app` in the existing `citadeliq-pg` container, set both connection strings through user-secrets, and run the API with `--launch-profile https`.
  - **Both connection strings are user-secrets of `CitadelIQ.Api`.** It is the only project with a `UserSecretsId`. `CitadelIQ.FluentMigrations` is a class library with no configuration of its own: `Program.cs` reads `ConnectionStrings:CitadelIQMigrations` and passes it to `AddFluentMigrations(...)`, and the migrations run inside the API process at startup.
  - **The owner login is the existing `postgres` superuser** that the `pgvector/pgvector` image creates from `POSTGRES_PASSWORD` (compose uses `citadeliq` via `POSTGRES_USER`). Nothing is created for it; only `citadeliq_app` is new.
  - Developers without Entra set `Authentication:Mode=DevelopmentLogin`.

---

## 11. Testing strategy

- **`TestApp` runs as the restricted role.**
  - `PostgresFixture` creates `citadeliq_app` once.
  - Migrations run with the superuser connection; services use an app-role connection string.
  - The `DatabaseRoleGuard` is exercised by a test that starts with the superuser and expects the startup check to throw.
- **Test identity.**
  - `TestApp.AsUser(workspaceId, userId, role)` returns a scope with `CurrentUserContext` filled in.
  - Helpers: `CreateIndividualAsync(name)`, `CreateOrganizationAsync(name, owner)`, `AddMemberAsync(org, role)`.
- **Isolation suite** (`Integration/WorkspaceIsolationTests`), with two workspaces A and B, each holding the same file contents. As A:
  - every service entry point (contents, get folder, create in B's folder, rename/delete B's folder, upload to B's folder, status/download/delete B's document, search in all three scopes, Ask) yields `NotFoundException`, or results containing only A's documents
  - `EntireWorkspace` search returns only A's chunks, even when B's chunks are closer to the query
- **RLS-alone test.** Through a raw app-role connection with `app.workspace_id` set to A:
  - `SELECT` from all three tables returns only A's rows
  - with the setting empty, it returns no rows
  - an `INSERT` with B's `WorkspaceId` fails the policy

  This proves the second layer works even without EF filters.
- **Composite-FK test.** Inserting a document into another workspace's folder fails at the database.
- **Background processing.** `Dispatch(workspaceId, …)` processes under RLS, so chunks get the right `WorkspaceId`. Processing a document under the wrong workspace finds nothing.
- **Membership rules:**
  - unit tests for `MembershipRules` (`CanManage` matrix, `EnsureOwnerRemains`) and `JoinCode`
  - integration tests: last Owner cannot demote, remove or leave; Admin cannot touch an Owner; approve, reject, cancel; join code regeneration
  - **Concurrency:** two Owners of a two-Owner org demote each other in parallel (`Task.WhenAll`). Exactly one succeeds and one Owner remains. Repeated 20× to catch flakiness.
- **Roles.** A Member gets `ForbiddenException` for document delete, folder rename and folder delete; an Admin succeeds.
- **API tests** (`WebApplicationFactory`):
  - a `TestAuthHandler` scheme reads `X-Test-User: iss|sub`
  - covered: 401 when signed out, 403 when closed, 403 during onboarding, `/api/me` states, the CSRF header requirement (400 without it), 403/404 status codes, per-user rate limiting, the join-code limiter (429)
  - the existing `AnswersApiTests` are updated to send a test user and the CSRF header
- **Frontend.** No test framework yet (unchanged). Manual QA covers the gating pages and the admin page.

---

## 12. Open questions / risks

1. *(Resolved)* In-flight streams on removal finish (decision 21). Accepted.
2. **Entra External ID specifics are unverified:**
   - authority/issuer format
   - whether `email` and `name` are present in the ID token with the chosen user flow
   - end-session behaviour
   - whether `localhost` HTTPS redirect URIs are accepted

   Phase 0 confirms these before any code depends on them.
3. *(Resolved)* Re-signing up with the same email creates a new account with no link to the old history (decision 20).
4. **Database reset required.** Rewriting the initial migration means every existing dev database (local container, any compose volume) must be dropped and recreated, and its stored files cleared. The PR and README must say so. Accepted: everything is development data.
5. **Recall under RLS.** Iterative scan should keep recall, but `hnsw.max_scan_tuples` (default 20,000) caps the scan. A very small workspace in a very large table could still get fewer than K results. Measure in Phase 2. Fallbacks: raise `max_scan_tuples` for the query, or partition `DocumentChunks` by workspace later.
6. **`__Host-` cookie + HTTPS in dev.** Requires the dev certificate (`dotnet dev-certs https --trust`) and the Vite dev server on HTTPS (`@vitejs/plugin-basic-ssl`, or proxying to the HTTPS backend with `server.https`). If that proves awkward, use the cookie name without the prefix in Development only.
7. **Two roles to operate.** Two passwords and a compose init script that only runs on a fresh volume. Existing volumes need the role created by hand (README step). The startup guard turns a misconfiguration into a clear failure instead of a silent bypass.
8. **The display name comes from the IdP** and is editable only there. Acceptable for now.

---

## 13. Out of scope

As listed in the requirements: organization SSO, domain verification and auto-join, multiple workspaces per user and a switcher, converting Individual → Organization, email invites, moving documents between workspaces, folder- or document-level permissions, deleting workspaces, billing, viewing an audit log, and API keys. §4 decision 25 records how SSO slots in later.

---

## 14. Implementation phases

0. **Entra spike (no code):**
   - Create the external tenant, user flow and app registration.
   - Use the minimal OIDC sample flow, or a scratch ASP.NET app, to record the real `iss`, `sub`, `email` and `name` claims, the authority URL, the end-session behaviour and the localhost redirect handling.
   - Write the findings into §12 and the README draft.
1. **Schema + domain:**
   - Rewrite the initial migration (§6): identity tables, workspace columns, composite FKs, RLS, role/grants; no Home seed. Reset the local dev database.
   - New and changed entities, `MembershipRules`, `JoinCode`, EF configurations.
   - Update `MigrationTests`.
   - Verify: migration up on a fresh DB, plus the RLS-alone and composite-FK tests.
2. **Workspace plumbing:**
   - `ICurrentUser`/`IWorkspaceContext`/`CurrentUserContext`.
   - Query filters, `WorkspaceConnectionInterceptor`, `DatabaseRoleGuard`.
   - Remove `Folder.RootId` and add `GetRootAsync`.
   - Storage path prefix and dispatcher signature.
   - `TestApp` on the app role with `AsUser`.
   - Verify: the whole existing suite passes inside one workspace, the isolation suite passes, and `EXPLAIN ANALYZE` of the vector query is checked.
3. **Authentication (BFF):**
   - Cookie + OIDC + DevelopmentLogin.
   - `AuthController`, `CurrentUserMiddleware`, CSRF middleware, fallback/`Authenticated` policies.
   - Exception mappings, per-user rate limiting, both connection strings.
   - Verify: API tests with `TestAuthHandler`, plus a manual sign-in with DevelopmentLogin.
4. **Onboarding + organization administration:**
   - `AccountService`, `OnboardingService`, `OrganizationAdminService`, repositories, `RunLockedAsync`, controllers, join limiter.
   - Verify: the membership-rule, concurrency and join-flow tests.
5. **Roles on content + uploader:**
   - `EnsureRole` in the folder and document services.
   - `UploadedBy` through documents, search and Ask.
   - Verify: role tests and uploader display tests (including a former member).
6. **Frontend:**
   - Vite proxy and relative URLs, CSRF header, 401 handling.
   - `SessionProvider`/`SessionGate` and the four gating pages.
   - Account menu, role-aware cards, uploader captions, `AdminPage`, the "Entire workspace" label.
   - Verify: `npm run build` and `npm run lint`, then a manual walk-through with DevelopmentLogin and two browser profiles (Owner + joiner).
7. **Deployment + docs:**
   - nginx proxy config, compose init script and env vars, `.env.example`.
   - README (Entra setup, DB roles, HTTPS dev), `CLAUDE.md` (architecture, config table, API contract, schema-change convention, removal of `Folder.RootId`, "expose only over HTTPS").
   - Extend `block-appsettings-secrets.py` to cover `ClientSecret`.
8. **Manual QA against the real Entra tenant**:
   - sign-up with email verification, all three onboarding paths, approval/rejection
   - role changes and the last-Owner rule from two browsers
   - removal → "account closed"
   - sign-out → re-prompt
   - cross-workspace URL tampering (copying a document's download URL into another account's browser → 404)

---

## 15. Definition of done

- Every item in the requirements' definition of done.
- The isolation suite passes for every endpoint, and the RLS-alone test proves database-level isolation.
- The API refuses to start with a superuser or `BYPASSRLS` runtime role.
- No access token is stored in the browser or the cookie, and every non-GET API call requires `X-CSRF`.
- The last-Owner concurrency test passes repeatedly.
- `dotnet test`, `npm run build` and `npm run lint` pass.
- The Docker stack runs end-to-end behind nginx over HTTPS.
- README and `CLAUDE.md` are updated.
- `SearchScope.EntireWorkspace` replaces `EntirePortal` everywhere (code, tests, `CLAUDE.md`, README, `design.md` note).
