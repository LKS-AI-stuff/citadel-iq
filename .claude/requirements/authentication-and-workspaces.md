# Requirements — Authentication, Accounts and Workspaces

> This file is the "what" and "why" only. All "how" — identity-provider setup, session mechanics, schema,
> tenant filtering, row-level security, endpoints, configuration keys and migration of existing data — belongs
> in the technical design (to be written: `.claude/technical-designs/authentication-and-workspaces.md`).
> Builds on the current application: folders, documents, search and Ask (RAG) all work today, but with **no
> authentication** — every endpoint is open and every document lives in one shared space under a single
> global "Home" folder.

## Objective

Make CitadelIQ a secure, multi-user product. People sign up and sign in; every document belongs to exactly one
**workspace**; and nobody can see, search, ask about, download or change anything outside their own workspace.
Two kinds of account are supported:

- **Individual account** — a private workspace with its own folder hierarchy, used by one person.
- **Organization account** — a workspace shared by the members of an organization: they all see the same
  folders and documents, and search and Ask cover the organization's documents.

## Problem statement

Today anyone who can reach the API can read, download, search, ask about and delete every document. That rules
out any real use with sensitive documents and any use by more than one person or company. The product needs
identities, a clear ownership boundary for data, and roles that control who may manage members and delete content.

## Concepts

- **User** — a person who signs in. Has exactly **one** workspace membership (see decisions).
- **Workspace** — the unit of data ownership and isolation. Every folder, document, chunk and stored file belongs
  to exactly one workspace. A workspace is either **Individual** (one member, ever) or **Organization** (many
  members). Each workspace has its own root "Home" folder.
- **Role** (per membership) — **Owner**, **Admin** or **Member**. Each role includes everything the role below it
  can do: Owner ⊃ Admin ⊃ Member. The user of an individual workspace is its Owner.
- **Join code** — an organization's identifier that a new user enters to ask to join it. Shared out-of-band by the
  organization's Owners/Admins; never discoverable from inside the product.
- **Join request** — a pending request from a newly signed-up user to join an organization, awaiting approval.

## Users and key scenarios

1. **Sign up as an individual** — a new person creates a login, chooses "Individual", and lands in an empty
   private workspace as its Owner.
2. **Sign up and create an organization** — a new person creates a login, chooses "Create organization", gives it
   a name, and lands in the new, empty organization workspace as its **Owner**.
3. **Sign up and join an organization** — a new person creates a login, chooses "Join organization" and enters a
   join code. They see a "waiting for approval" page and cannot reach any documents until an Owner or Admin
   approves. If rejected, they return to the onboarding choice.
4. **Approve or reject a join request** — an Owner or Admin opens the admin page, sees pending requests and
   approves (choosing the role, Member by default) or rejects each one.
5. **Manage members** — an Owner or Admin sees the member list (name, email, role, joined date), changes roles and
   removes members, within the rules below.
6. **Work as a member** — a Member browses, uploads, searches and asks across the organization's documents but
   cannot delete documents or folders, or manage members.
7. **Remove a member / leave** — a removed (or departing) member immediately loses all access. Documents they
   uploaded stay with the organization. Their login is closed for good: to use CitadelIQ again they must sign up
   with a new email.
8. **Sign in / sign out** — a returning user signs in and lands in their workspace; signing out ends the session.
9. **Cross-workspace attempts** — a user who obtains the id of a folder or document from another workspace (a
   guessed id, an old link) gets a "not found" response, exactly as if it did not exist.

## Functional requirements

### Sign-up, sign-in and sessions
- Sign-up, sign-in, password reset and email verification are handled by a hosted identity provider; CitadelIQ
  never sees or stores passwords. Email must be verified before onboarding.
- After the first sign-in, the user completes an **onboarding** step in CitadelIQ: Individual, Create
  organization, or Join organization (by join code). Until onboarding is complete and the user has an active
  membership, they see only the onboarding / waiting / closed-account pages; every data API rejects them.
- A user is identified by the identity provider's stable account identifier, **not** by email address.
- Sessions expire after inactivity and have a maximum lifetime (values configurable). Sign-out ends the session
  in CitadelIQ.
- No access tokens are readable by the browser's JavaScript; the browser holds only a secure, HTTP-only session
  cookie (see decisions).

### Workspaces and isolation (the core security requirement)
- Every folder, document, chunk, embedding and stored file belongs to exactly one workspace.
- **Every** operation is limited to the caller's workspace: browsing folders, folder contents and breadcrumbs,
  upload, processing status, download, delete, rename, search (all three scopes), Ask (retrieval, sources and
  citations) and the stored files themselves. The "Entire portal" search scope is renamed **"Entire workspace"** and covers the caller's whole workspace
  hierarchy (everything the user can access; there are no folder-level permissions).
- Requests for another workspace's items return **404 Not Found**, never 403, so ids from other workspaces are
  not confirmed to exist.
- Isolation is enforced in the application **and** by the database itself, so a single missing filter in code
  cannot leak another workspace's data.
- Background document processing runs in the context of the document's workspace.
- Each workspace has its own root "Home" folder; folder-name uniqueness is per parent folder, as today.

### Roles and permissions

| Action | Member | Admin | Owner |
|---|---|---|---|
| Browse folders, view documents, download | ✓ | ✓ | ✓ |
| Search and Ask (within the workspace) | ✓ | ✓ | ✓ |
| Upload documents, create folders | ✓ | ✓ | ✓ |
| Rename folders | — *(assumed, see open questions)* | ✓ | ✓ |
| Delete documents and folders | — | ✓ | ✓ |
| See the admin page, join code and member list | — | ✓ | ✓ |
| Approve / reject join requests | — | ✓ | ✓ |
| Change roles between Member and Admin; remove Members/Admins | — | ✓ | ✓ |
| Promote to / demote from Owner; remove an Owner | — | — | ✓ |
| Regenerate the join code | — | ✓ | ✓ |

- Permissions are enforced on the server; the UI additionally hides actions the user cannot perform (e.g. no
  Delete buttons for Members, no admin link for Members or individual accounts).
- Role and membership changes take effect on the user's **next request** — not at their next sign-in.

### Admin page (organization workspaces only)
- Visible to Owners and Admins. Shows: the member list (name, email, role, joined date), pending join requests,
  and the organization's join code (with a "regenerate" action that invalidates the old code).
- Owners and Admins can change roles and remove members within the permission table above.
- **Invariant: an organization always has at least one Owner.** Because every Owner is also an Admin, this also
  guarantees at least one Admin. Any change that would leave zero Owners — demoting, removing, or leaving as the
  last Owner — is rejected with a clear message.
- The invariant is enforced on the server and holds under concurrent changes (e.g. two Owners demoting each other
  at the same time must not both succeed). The UI also prevents it, for a friendlier message.

### Joining
- A join code is opaque, hard to guess and can be regenerated. There is no list or search of organizations.
- Entering an invalid code shows a generic "code not recognised" message.
- A user has at most one pending join request at a time and can cancel it (returning to onboarding).
- Approval adds the user to the organization with the chosen role (default Member).

### Leaving and removal
- Removal is immediate: the removed user's next request is rejected. (An Ask answer already streaming may finish.)
- Documents and folders the user created remain with the organization, and still show them as the uploader
  (marked as a former member).
- The removed user's CitadelIQ account is permanently closed; signing in with the same login shows an "account
  closed" page. Coming back requires signing up with a new email (a new account).

### Uploader shown on documents
- Every document records the user who uploaded it. The document card and search/Ask source cards show the
  uploader's display name; documents uploaded by a removed member show their name marked "former member".
- In individual workspaces the uploader is always the owner, so it is not shown there.
- The uploader's email is shown only on the admin page, not on documents.

### Abuse protection
- Rate limits currently applied per IP (Ask) are applied per **user** once signed in.
- Join-code attempts are rate-limited to prevent guessing.

## Non-functional requirements

- **Clean Architecture preserved** — the current user and workspace are available to the Application layer
  through an interface; the identity-provider libraries are confined to the API/Infrastructure layers; business
  rules (roles, last-Owner invariant, join approval) live in the Application layer, not controllers.
- **Defence in depth** — workspace isolation is enforced by both application code and the database.
- **Portable hosting** — the application keeps running in Docker with PostgreSQL + pgvector as today; only login
  is delegated to the hosted identity provider.
- **Extensible to organization SSO later** (see out of scope) without reworking accounts, workspaces or roles.
- **Safe failures** — authentication and authorization failures return safe ProblemDetails (401/403/404) with no
  internal detail; the identity provider being unreachable produces a friendly sign-in error.
- **Privacy** — logs may record user and workspace ids and outcomes, never document content, questions or answers
  (unchanged from RAG).
- **Testability** — authentication can be replaced in tests by a fake signed-in user with a chosen workspace and
  role; automated tests cover cross-workspace isolation for every endpoint (including search, Ask and download),
  role enforcement, the join flow and the last-Owner invariant under concurrency.
- **Existing behaviour unchanged** inside a workspace — folders, upload, processing, search and Ask behave
  exactly as today, just scoped to the workspace.

## Out of scope for this phase

Organization single sign-on (SAML/OIDC federation with a company's own identity provider), domain verification
and automatic joining by email domain, a user belonging to more than one workspace (and a workspace switcher),
converting an individual account into an organization, inviting users by email, transferring a document between
workspaces, folder-level or document-level permissions, deleting an organization or individual workspace,
billing, audit-log viewing, and API keys for programmatic access.

**Designed to be added later:** organization SSO. It plugs in as a per-organization sign-in connection plus
verified domains; users signing in through it are added to that organization automatically instead of through a
join request. The join-code + approval path remains for organizations without SSO.

## Decisions made

1. **Account types:** Individual and Organization, both modelled as a workspace; one workspace per user.
2. **Identity provider:** Microsoft Entra External ID (hosted), integrated through standard OIDC with the .NET
   authentication libraries. The application itself keeps running in Docker with PostgreSQL + pgvector.
3. **Session model:** backend-for-frontend — the API performs the sign-in and issues an HTTP-only session
   cookie; the UI calls the API on the same origin. No tokens in browser JavaScript.
4. **Membership and roles** live in CitadelIQ's database, not in the identity provider.
5. **Isolation:** a workspace column on all workspace-owned data, filtered in the application and enforced by
   database row-level security.
6. **Onboarding:** after sign-up the user chooses Individual / Create organization / Join organization.
   Creating an organization makes the user its Owner.
7. **Joining:** by join code plus Owner/Admin approval; no organization list.
8. **Roles:** Owner ⊃ Admin ⊃ Member; at least one Owner always (which implies at least one Admin).
9. **Deletion rights:** only Admins and Owners delete documents and folders.
10. **Leaving/removal:** documents stay with the organization; the user's account is permanently closed and
    re-joining requires a new account with a new email.
11. **Search scope:** "Entire portal" is renamed "Entire workspace".
12. **Removal during an answer:** an Ask answer already streaming when a member is removed may finish; every later
    request is rejected.
13. **Rejoining with the same email** creates a new account; previously uploaded documents stay attributed to the
    old (closed) account and are never re-linked.
14. **Uploader:** shown on documents in organization workspaces (display name; "former member" after removal).
15. **Open-question assumptions** below were confirmed by the product owner.

## Confirmed assumptions

1. **Folder rename by Members** — [not allowed; Admins and Owners only, like delete].
2. **Existing data**: today's documents are development data. Existing databases are reset; no data is migrated
   into a workspace.
3. **MFA** — [available through the identity provider and optional per user; not enforced].
4. **Session lifetimes** — [idle timeout 8 hours, maximum lifetime 7 days; configurable].
5. **Membership change history** — [not recorded in this phase].

## Definition of done

- [ ] A new user can sign up, verify email, and onboard as Individual, as creator (Owner) of a new organization,
      or as a join requester; until approved they cannot reach any data.
- [ ] Owners/Admins can approve or reject join requests, change roles, remove members and regenerate the join
      code; Members and individual accounts have no admin page.
- [ ] No change can leave an organization without an Owner, including concurrent changes.
- [ ] Organization documents show their uploader, including after the uploader is removed.
- [ ] Members cannot delete documents or folders (server-enforced, and the UI hides the actions).
- [ ] Removed users lose access on their next request and see an "account closed" page on sign-in.
- [ ] Automated tests prove that, for every endpoint (folders, documents, download, status, search in all three
      scopes, Ask), a user cannot see or affect another workspace's data, and get 404 for its ids.
- [ ] Workspace isolation holds at the database level even if an application-level filter is omitted.
- [ ] No access token is readable from browser JavaScript.
- [ ] Inside a workspace, folders, upload, processing, search and Ask behave as they do today.
- [ ] README and design document describe the Entra External ID setup and the new configuration.
