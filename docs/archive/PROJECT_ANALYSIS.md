# Docly Project Analysis & Blazor Migration Plan

## Project Overview

**Docly** is a document intelligence application that allows users to upload documents (PDF, DOCX, TXT) and ask natural language questions about them.

### Current Architecture

```
Frontend (Vanilla HTML/CSS/JS)
        ↓
API (ASP.NET Core 10)
        ↓
Services Layer (DDD/Clean Architecture)
        ↓
PostgreSQL + pgvector (Embeddings)
```

## Current Tech Stack

| Component | Technology | Purpose |
|-----------|-----------|---------|
| Frontend | HTML5, CSS3, Vanilla JS | UI for auth, doc upload, chat |
| Backend API | ASP.NET Core 10 | REST endpoints (auth, docs, chat, search) |
| Database | PostgreSQL | Document storage, user data, embeddings |
| Vector DB | pgvector extension | Semantic search embeddings (768-dim) |
| AI Models | Ollama (local) | Text embeddings, answer generation |
| Authentication | JWT (Bearer tokens) | User auth & authorization |

## Current Database Schema

### Tables
1. **Users** - Auth credentials
   - Id (Guid), Email, PasswordHash, CreatedAtUtc

2. **Documents** - Uploaded files
   - Id, Title, Filename, StorageKey, ContentType, FileSizeBytes, Status, UserId, ProcessingError, CreatedAtUtc
   - Statuses: PendingExtraction → Extracting → Chunking → Embedding → Completed (or Failed)

3. **DocumentChunks** - Text segments with embeddings
   - Id, DocumentId, Content, ChunkIndex, Embedding (vector), CreatedAtUtc

### Missing: Chat History
- No table for storing conversation history
- Current chat is stateless (each question is independent)

## Current UI Components

### Auth View
- Login/Register tabs
- Email & password form
- JWT token storage in localStorage

### Workspace (Post-Auth)
- **Sidebar**: Documents list, new chat button, sign out
- **Upload Panel**: Drag-drop file upload with title input
- **Chat Section**: Message thread, empty state with suggestions, chat composer
- **Real-time Status**: Document processing status updates (10s polling)

## Current API Endpoints

```
POST   /api/auth/register              - User registration
POST   /api/auth/login                 - User login
GET    /api/documents/                 - List user's documents
POST   /api/documents/                 - Upload document
GET    /api/search/?query=X&topN=5     - Semantic search
POST   /api/chat/                      - Ask question (stateless)
```

## What's Working Well

✅ Clean .NET architecture (Domain → App → Infra → Api)
✅ JWT authentication with rate limiting
✅ pgvector semantic search
✅ Document processing pipeline (background service)
✅ Responsive UI design
✅ Error handling middleware
✅ Tests in place (Infra & App layers)

## Migration Scope: HTML/CSS/JS → Blazor

### What to Build

1. **Blazor WebAssembly Project** (new)
   - Component hierarchy mirroring current HTML structure
   - Reusable components (buttons, forms, messages)
   - Client-side state management

2. **Chat History Feature** (new)
   - Database: ChatConversation & ChatMessage tables
   - API endpoints for CRUD + search
   - Blazor UI for history list, tagging, search

3. **Migrate Components**
   - AuthView → Auth.razor
   - Workspace → Workspace.razor
   - DocumentList → DocumentList.razor
   - ChatSection → ChatSection.razor
   - MessageThread → MessageThread.razor
   - UploadPanel → DocumentUpload.razor

4. **Styling**
   - Import current CSS → global stylesheet
   - Keep existing design, enhance with Blazor interactivity

### Database Changes

**New Tables:**

```sql
CREATE TABLE ChatConversations (
    Id GUID PRIMARY KEY,
    UserId GUID NOT NULL FOREIGN KEY,
    Title VARCHAR(255),
    CreatedAtUtc DATETIME NOT NULL,
    UpdatedAtUtc DATETIME NOT NULL,
    Tags JSONB -- ["contract", "summary"]
);

CREATE TABLE ChatMessages (
    Id GUID PRIMARY KEY,
    ConversationId GUID NOT NULL FOREIGN KEY,
    Role VARCHAR(50), -- "user" or "assistant"
    Content TEXT NOT NULL,
    SourceDocuments JSONB, -- [{DocumentId, DocumentTitle, SimilarityScore}]
    CreatedAtUtc DATETIME NOT NULL
);
```

### New API Endpoints

```
GET    /api/conversations/             - List user's conversations
POST   /api/conversations/             - Create new conversation
GET    /api/conversations/{id}/messages - Get conversation messages
POST   /api/conversations/{id}/messages - Add message to conversation
PATCH  /api/conversations/{id}         - Update tags/title
DELETE /api/conversations/{id}         - Delete conversation
GET    /api/conversations/search?q=X   - Search conversations by content/tags
```

## Migration Steps (High Level)

1. **Phase 1: Setup**
   - Create Docly.Web (Blazor WASM project)
   - Add ChatHistory domain models & migrations
   - Create new EF migrations

2. **Phase 2: API Layer**
   - Implement ChatConversationEndpoints & ChatMessageEndpoints
   - Add ChatConversationRepository & ChatMessageRepository
   - Add search service for chat history

3. **Phase 3: Blazor Components**
   - Create component library
   - Migrate UI piece by piece
   - Integrate with API

4. **Phase 4: Chat History UI**
   - History sidebar/panel
   - Search & filter
   - Tag management
   - Create/switch conversations

5. **Phase 5: Polish & Deploy**
   - Testing
   - Error handling
   - Performance optimization
   - Docker deployment

## Key Decisions

| Decision | Rationale |
|----------|-----------|
| Blazor WASM | Keeps everything in .NET, interop with backend, faster DX |
| Keep API as-is | Chat endpoint stays stateless, new endpoints for history |
| Add Migration | Chat history is new feature, requires db schema |
| Keep Ollama local | No change to AI infrastructure |
| JWT auth unchanged | Already working, compatible with Blazor |
| PostgreSQL for history | Consistency with existing stack, JSONB for tags |

## Files to Create

### Backend
- `Docly.Domain/Entities/ChatConversation.cs`
- `Docly.Domain/Entities/ChatMessage.cs`
- `Docly.App/Interfaces/IChatConversationRepository.cs`
- `Docly.App/Interfaces/IChatMessageRepository.cs`
- `Docly.App/Interfaces/IChatHistorySearchService.cs`
- `Docly.Infra/Repositories/ChatConversationRepository.cs`
- `Docly.Infra/Repositories/ChatMessageRepository.cs`
- `Docly.Infra/Search/ChatHistorySearchService.cs`
- `Docly.Api/Endpoints/ChatConversationEndpoints.cs`
- `Docly.Infra/Migrations/[timestamp]_AddChatHistory.cs`

### Frontend
- `Docly.Web/` (new project)
- `Docly.Web/Components/Auth.razor`
- `Docly.Web/Components/Workspace.razor`
- `Docly.Web/Components/DocumentList.razor`
- `Docly.Web/Components/ChatSection.razor`
- `Docly.Web/Components/MessageThread.razor`
- `Docly.Web/Components/DocumentUpload.razor`
- `Docly.Web/Components/ChatHistory.razor`
- `Docly.Web/Services/ApiService.cs`
- `Docly.Web/Styles/app.css` (migrated from wwwroot)

---

**Next Step**: Ready for detailed implementation plan? I can either:
1. Deep-dive into Phase 1 (setup & domain models)
2. Show you the full implementation plan for all phases
3. Start coding the first component
