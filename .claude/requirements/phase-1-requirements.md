# Build a Professional Document Search Portal — "DocuSearch"

Build a full-stack **Document Search Portal** using:

- **Backend:** ASP.NET Core Web API with C#
- **Architecture:** Clean Architecture
- **Frontend:** React + TypeScript
- **UI:** Material UI (MUI) + Tailwind CSS
- **AI:** Official OpenAI .NET SDK
- **Current vector storage:** In-memory/session-based storage
- **Future vector storage:** PostgreSQL + pgvector
- **Future persistence layer:** Separate infrastructure/persistence implementation
- **Future security:** Authentication, authorization, user-specific document spaces

This application is an AI-powered document management and semantic search portal. It should
look and feel like a professional business application, **not like a chat application**.

## 1. Primary Business Objective

The portal allows users to upload documents, organize them into folders, and search the contents
of those documents using natural-language questions.

The search should use **semantic search** rather than simple keyword matching.

For example, if a document contains:

"Employees are eligible for 15 days of annual vacation."

A user should be able to search:

"How many vacation days can an employee take?"

and receive the relevant document section even though the exact words in the question do not
match the document.

The application should demonstrate the complete semantic-search process:

1. Upload document
2. Extract text
3. Split text into chunks
4. Generate embeddings for each chunk using OpenAI
5. Store document metadata, chunks, and embeddings in memory/session storage
6. Accept a natural-language search query
7. Generate an embedding for the query
8. Calculate similarity between the query embedding and document chunk embeddings
9. Sort results by similarity
10. Return the most relevant document chunks and similarity scores

**Do not generate an AI-written answer from the search results yet.**

The current version is a **semantic document search system**, not a RAG chatbot.

## 2. Document Management UI

The portal should have a professional document-management interface.

The default location when the user opens the application is:

**Home**

The main content area displays folders and files.

Example:

```
Home
• Finance
  ◦ 2025 Reports
    ▪ January.pdf
    ▪ February.pdf
  ◦ Budget.xlsx
• HR
  ◦ Employee Handbook.pdf
• Policies
  ◦ Vacation Policy.pdf
```

Folders and files should be visually distinct.

Use professional icons, spacing, typography, cards/list views, breadcrumbs, hover states, and
contextual actions.

## 3. Folder Navigation

When the user clicks a folder:

- Navigate into that folder.
- Keep the same overall screen layout.
- Display the files and folders contained within the selected folder.
- Show a breadcrumb so the user understands their current location.

Example:

Home / Finance / 2025 Reports

The main content area then displays:

- January.pdf
- February.pdf

Clicking a folder should NOT open a completely different page design.

The document-management experience should remain consistent throughout the application.

## 4. File Interaction

When the user clicks a file:

- Download the original file.
- Preserve the original filename.
- The document should remain associated with its extracted text, chunks, and embeddings.

Provide appropriate file icons based on file type.

Do not allow video uploads.

## 5. File Upload

Users must be able to upload documents through a professional upload experience.

Provide:

- Upload button
- Drag-and-drop upload area
- Upload progress/status
- File validation
- Clear success/error messages
- Selected destination folder
- Ability to cancel an upload where practical

Supported file types should initially be restricted to document-oriented formats.

For example:

- PDF
- DOCX
- TXT
- CSV
- XLSX

Do NOT support:

- MP4
- AVI
- MOV
- MKV
- Other video formats

Do not blindly accept every file extension.

Create a configurable allowed-file-type list so additional formats can be added later.

Also implement a configurable maximum file size.

The exact maximum size should be defined in configuration rather than hardcoded throughout
the application.

For example:

```
MaxFileSizeMB = configurable value
AllowedExtensions = configurable list
```

Reject unsupported or oversized files before attempting document processing.

## 6. Document Processing

After a document is uploaded:

1. Validate the file.
2. Extract its text.
3. Split the text into meaningful chunks.
4. Generate an embedding for every chunk using OpenAI.
5. Store the document metadata, chunks, and embeddings in memory.

Do NOT create one embedding for the entire document.

Each searchable chunk should have a relationship similar to:

```
Document
 |
 +-- Chunk 1
 |     +-- Text
 |     +-- Embedding
 |
 +-- Chunk 2
 |     +-- Text
 |     +-- Embedding
 |
 +-- Chunk 3
       +-- Text
       +-- Embedding
```

Keep the chunk size and overlap configurable.

Display processing status to the user.

For example:

```
Uploading...
Extracting text...
Creating searchable sections...
Generating embeddings...
Ready to search
```

The UI should not expose API keys or internal exceptions.

## 7. Search Experience

The search experience should be integrated into the document-management portal.

The search sidebar should be **hidden by default**.

There should be a search icon/button in the main application UI.

When the user clicks the search icon:

- A search panel should slide into the screen.
- The panel should occupy approximately half of the screen.
- The underlying document-management UI should remain visible.
- The search panel should be dismissible.

This should feel like a professional enterprise application rather than a chat window.

## 8. Search Panel

The search panel should contain:

**Search Input**

A natural-language search box.

Example:

```
How many vacation days are employees allowed?
```

Provide:

- Search button
- Enter-to-search behavior
- Loading state
- Clear button where appropriate

## 9. Search Scope

The search panel must provide three mutually exclusive search scopes.

Use **radio buttons or another UI control that makes the three choices immediately
understandable**.

**Option 1 — Entire Portal**

Search all documents available to the current user.

**Option 2 — Current Folder**

Search only documents directly inside the folder currently being viewed.

**Option 3 — Current Folder + Subfolders**

Search documents inside the current folder and all of its descendant folders.

For example, if the user is currently at:

```
Home / Finance / 2025
```

then:

**Entire Portal**

Search everything.

**Current Folder**

Search only documents directly inside:

```
2025
```

**Current Folder + Subfolders**

Search:

```
2025
├── January
├── February
└── March
```

and all documents contained within those folders.

The search service should receive the search scope and current folder ID rather than having the
frontend implement the search rules.

## 10. Search Results

Search results should be professional and easy to scan.

Each result should display information such as:

- Document name
- Folder/location
- Relevant text/chunk
- Similarity score
- Chunk number
- Optional page number when available
- File type icon

Sort results by similarity score descending.

Example:

```
Search Results

Employee Handbook.pdf
HR / Policies

"Employees are eligible for 15 days of annual vacation..."

Similarity: 0.91

--------------------------------

Vacation Policy.pdf
HR / Policies

"Full-time employees receive fifteen vacation days..."

Similarity: 0.87
```

The similarity score should be clearly labeled.

Do not present the similarity score as a probability.

## 11. Current AI Scope

Use the official OpenAI .NET SDK.

Use an embedding model appropriate for semantic search, currently:

```
text-embedding-3-small
```

Use the OpenAI SDK's embedding functionality to:

- Generate document chunk embeddings.
- Generate search-query embeddings.

The application itself should implement:

- Cosine similarity
- Result ranking
- Top-K selection
- Search filtering by folder/scope

Do not introduce a vector database yet.

Do not introduce Semantic Kernel yet.

Do not introduce LangChain.

Do not build a RAG answer-generation pipeline yet.

Do not use an LLM to summarize the search results in this version.

The purpose of this version is to clearly demonstrate how embeddings and semantic similarity
work.

## 12. Backend Clean Architecture

Use Clean Architecture for the C# backend.

Organize the solution into logical projects such as:

```
DocumentSearch.Api
DocumentSearch.Application
DocumentSearch.Domain
DocumentSearch.Infrastructure
```

Responsibilities:

### Domain

Contains:

- Entities
- Value objects where appropriate
- Domain concepts
- Business rules

Examples:

```
Document
Folder
DocumentChunk
DocumentEmbedding
```

### Application

Contains:

- Use cases
- Interfaces
- DTOs
- Application services
- Search logic
- Validation rules
- Commands/queries where appropriate

Examples:

```
UploadDocument
CreateFolder
GetFolderContents
SearchDocuments
DownloadDocument
ProcessDocument
```

### Infrastructure

Contains external implementations.

For the current version, keep persistence abstractions ready but do not require a database.

Examples:

```
IOpenAIEmbeddingService
IDocumentStorage
IDocumentRepository
IFolderRepository
```

Implement the current storage using in-memory/session-based storage.

The architecture should make it easy to replace the in-memory implementation later with
PostgreSQL/pgvector without changing the Application layer.

### API

Contains:

- Controllers/endpoints
- Dependency injection
- HTTP concerns
- Authentication later
- API configuration

Keep business logic out of controllers.

## 13. Current Storage

For this version, use in-memory/session-based storage.

We need to store:

```
Folder
Document
Document metadata
Original file information
Extracted text/chunks
Embeddings
```

The design must make the storage implementation replaceable.

Do not tightly couple the application to the in-memory implementation.

## 14. Frontend Architecture

The React application should use a modular component architecture.

Use:

- React
- TypeScript
- Material UI (MUI)
- Tailwind CSS

Create reusable components rather than putting the entire application inside one large
component.

Potential components:

```
AppShell
TopNavigation
Sidebar
Breadcrumbs
FolderView
FolderCard
FileCard
FileList
FileIcon
UploadDialog
UploadDropzone
SearchPanel
SearchScopeSelector
SearchInput
SearchResults
SearchResultCard
ProcessingStatus
EmptyState
LoadingState
ErrorMessage
```

Create reusable hooks/services for API communication.

For example:

```
useDocuments()
useFolders()
useSearch()
useUpload()
```

Keep UI components separate from API/data-access logic.

## 15. Rich Professional UI

The UI should be significantly richer than a basic chat application.

Do NOT make the UI look like:

```
ChatGPT
[message input]
```

Instead, make it feel like a modern enterprise document-management application.

Consider:

- Responsive layout
- Top navigation
- Breadcrumb navigation
- File/folder cards or table/list
- Professional icons
- Drag-and-drop upload
- Search drawer/panel
- Smooth transitions
- Hover states
- Loading skeletons
- Empty states
- Error states
- Toast notifications
- Context menus
- File type icons
- Upload progress
- Search result highlighting
- Folder navigation
- Responsive behavior
- Dark/light theme support where practical

Use MUI for sophisticated components and accessibility.

Use Tailwind for layout, spacing, responsive utilities, and custom styling where appropriate.

Avoid creating conflicting styles between MUI and Tailwind.

Create a consistent design system.

## 16. API Design

Create clean REST APIs.

Examples:

```
GET    /api/folders/{folderId}
GET    /api/folders/{folderId}/contents
POST   /api/folders
POST   /api/documents/upload
GET    /api/documents/{documentId}/download
DELETE /api/documents/{documentId}
POST   /api/search
```

The exact endpoint structure can be adjusted if there is a better RESTful design.

The search request should contain information similar to:

```
query
currentFolderId
searchScope
topK
```

The backend should determine which documents are eligible for search based on the scope.

## 17. Error Handling

Handle errors professionally.

Examples:

- Unsupported file type
- File too large
- Empty document
- Unable to extract text
- OpenAI embedding failure
- Search attempted before documents are processed
- Empty search query
- Folder not found
- Document not found
- Download failure

Return user-friendly messages.

Never expose:

- API keys
- Stack traces
- Internal implementation details
- Sensitive configuration

## 18. Security — Future Scope

Security is intentionally **not the primary implementation for this version**, but the architecture
must prepare for it.

Future security requirements will include:

- User authentication
- Authorization
- User-specific document spaces
- User-specific folders
- User-specific documents
- Tenant/user isolation
- Role-based access
- Secure file downloads
- API authorization
- Audit logging

The future model should look conceptually like:

```
User
 |
 +-- Document Space
       |
       +-- Folder
       |     +-- Documents
       |
       +-- Folder
             +-- Documents
```

A user should eventually only be able to see and search documents they are authorized to access.

Do not design the current application in a way that makes user isolation difficult later.

## 19. Future Persistence and Vector Database

The current implementation intentionally uses in-memory storage.

Future implementation:

```
PostgreSQL
    +
pgvector
```

The future database should store:

- Users
- Folders
- Documents
- Document metadata
- Document chunks
- Embeddings
- Relationships between users/folders/documents

The vector database will eventually replace the current in-memory vector search.

The Application layer should not know whether embeddings are stored:

```
In Memory
```

or:

```
PostgreSQL + pgvector
```

This should be hidden behind interfaces.

## 20. Future RAG Capability

After semantic search is working correctly, the application can be extended to RAG.

Future flow:

```
User Question
      ↓
Query Embedding
      ↓
Vector Search
      ↓
Top Relevant Chunks
      ↓
LLM
      ↓
Generated Answer
      ↓
Citations / Sources
```

The current application should stop after:

```
Vector Search
      ↓
Relevant Results
```

This separation is intentional so we can clearly understand embeddings and semantic search
before introducing RAG.

## 21. Future Enhancements

Design the system so the following can be added later:

**Security**

- Authentication
- Authorization
- User-specific document spaces
- Roles/permissions
- Multi-tenancy

**Persistence**

- PostgreSQL
- Entity Framework Core
- pgvector
- Persistent document metadata
- Persistent embeddings

**Search**

- Metadata filtering
- File-type filtering
- Date filtering
- Advanced search
- Search history
- Pagination
- Hybrid keyword + vector search

**AI/RAG**

- RAG
- LLM-generated answers
- Source citations
- Page-level citations
- Context-window management
- Reranking
- Answer evaluation

**Document Management**

- Rename files
- Delete files
- Move files
- Rename folders
- Delete folders
- Bulk upload
- Bulk operations
- Document previews

**Enterprise Features**

- Audit logging
- Usage analytics
- Monitoring
- Error tracking
- Document processing queues
- Background processing
- Large-document processing
- Multiple document versions

## 22. Important Implementation Principles

Follow these principles throughout the implementation:

1. Keep the backend Clean Architecture.
2. Keep business logic out of controllers.
3. Keep React components modular.
4. Keep API/data-access code separate from UI components.
5. Use dependency injection.
6. Use interfaces around external services.
7. Do not tightly couple the Application layer to OpenAI.
8. Do not tightly couple the Application layer to in-memory storage.
9. Make the future PostgreSQL/pgvector migration straightforward.
10. Make file-size and file-type restrictions configurable.
11. Do not allow video uploads.
12. Do not expose API keys to the frontend.
13. Validate uploads on the backend even if the frontend validates them.
14. Use asynchronous APIs for file processing and OpenAI calls.
15. Return clear DTOs from APIs.
16. Use proper cancellation/error handling.
17. Avoid giant classes/components.
18. Prefer reusable services and components.
19. Build the UI as a professional document-management portal, not a chatbot.
20. Keep the current scope focused on **document management + embeddings + semantic
    search**.

## 23. Development Approach

Build the application incrementally.

First establish:

```
Backend Clean Architecture
          +
React/MUI/Tailwind application shell
          +
Folder/file UI
```

Then implement:

```
File upload
      ↓
Text extraction
      ↓
Chunking
      ↓
OpenAI embeddings
      ↓
In-memory storage
      ↓
Semantic search
      ↓
Search results
```

Do not implement future PostgreSQL, authentication, pgvector, or RAG prematurely.

However, structure the code so those capabilities can be added without major architectural
changes.

Before writing large amounts of code, create the proposed:

- Solution structure
- Project structure
- Domain entities
- Application interfaces
- API endpoints
- React component structure
- Data models

Then implement the application in logical stages.

---

The final application should demonstrate that a full-stack engineer understands both **professional
application architecture** and the underlying mechanics of **AI-powered semantic search**.
