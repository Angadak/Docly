# Setup & Deployment Guide

## Prerequisites

- .NET 10 SDK
- PostgreSQL 14+ (with pgvector extension)
- Ollama (for local LLM)
- Visual Studio 2022 or VS Code

## Database Setup

### 1. Create PostgreSQL Database

```bash
# Connect to PostgreSQL
psql -U postgres

# Create database
CREATE DATABASE docly;

# Connect to database
\c docly

# Install pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;

# Verify
\dx
```

### 2. Apply Migrations

```bash
cd Docly
dotnet ef database update --project Docly.Infra --startup-project Docly.Api
```

This will create:
- Users table
- Documents table
- DocumentChunks table
- ChatConversations table (NEW)
- ChatMessages table (NEW)

## Local Development Setup

### Backend (API)

```bash
cd Docly.Api

# Install dependencies
dotnet restore

# Update appsettings.Development.json with your config:
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=docly;Username=postgres;Password=YOUR_PASSWORD"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434"
  },
  "Jwt": {
    "Key": "your-super-secret-jwt-key-min-32-chars!!!",
    "Issuer": "docly-api",
    "Audience": "docly-app"
  }
}

# Run API
dotnet run

# API listens on https://localhost:7000
```

### Frontend (Blazor Web)

```bash
cd Docly.Web

# Build
dotnet build

# Run dev server
dotnet run

# Open https://localhost:7001
```

### Ollama Setup

```bash
# Start Ollama (if not running)
ollama serve

# In another terminal, pull required models
ollama pull nomic-embed-text  # For embeddings (768-dim)
ollama pull llama2:7b-chat    # Or your preferred model
```

Verify Ollama is working:
```bash
curl http://localhost:11434/api/tags
```

## Project Structure

```
Docly/
├── Docly.Domain/           # Entities (DDD)
│   └── Entities/
│       ├── User.cs
│       ├── Document.cs
│       ├── DocumentChunk.cs
│       ├── ChatConversation.cs (NEW)
│       └── ChatMessage.cs (NEW)
│
├── Docly.App/              # Business logic & interfaces
│   ├── Interfaces/
│   │   ├── IUserRepository.cs
│   │   ├── IDocumentRepository.cs
│   │   ├── IChatConversationRepository.cs (NEW)
│   │   ├── IChatMessageRepository.cs (NEW)
│   │   └── ...other services...
│   └── Models/
│       └── DTO models
│
├── Docly.Infra/            # Data access & services
│   ├── DbContexts/
│   │   └── AppDbContext.cs (updated)
│   ├── Repositories/
│   │   ├── ChatConversationRepository.cs (NEW)
│   │   ├── ChatMessageRepository.cs (NEW)
│   │   └── ...other repos...
│   ├── Migrations/
│   │   ├── 20260723000001_AddChatHistory.cs (NEW)
│   │   ├── 20260723000001_AddChatHistory.Designer.cs (NEW)
│   │   └── ...other migrations...
│   └── ...services...
│
├── Docly.Api/              # REST endpoints
│   ├── Program.cs (updated)
│   ├── Endpoints/
│   │   ├── AuthEndpoints.cs
│   │   ├── DocumentEndpoints.cs
│   │   ├── SearchEndpoints.cs
│   │   ├── ChatEndpoints.cs (ENHANCED)
│   │   ├── ChatConversationEndpoints.cs (NEW)
│   │   ├── ChatMessageEndpoints.cs (NEW)
│   │   └── ...other endpoints...
│   ├── Middleware/
│   │   └── GlobalExceptionHandlingMiddleware.cs
│   ├── wwwroot/ (OLD - will be replaced by Blazor)
│   ├── Properties/
│   └── appsettings.*.json
│
├── Docly.Web/              # Blazor WebAssembly (NEW)
│   ├── Docly.Web.csproj
│   ├── Program.cs
│   ├── App.razor
│   ├── _Imports.razor
│   ├── Components/
│   │   ├── Layout.razor
│   │   ├── Workspace.razor (NEW)
│   │   ├── ChatSection.razor (NEW)
│   │   ├── ChatHistory.razor (NEW)
│   │   └── DocumentUpload.razor (NEW)
│   ├── Pages/
│   │   └── Home.razor
│   ├── Services/
│   │   ├── ApiService.cs
│   │   └── AuthService.cs (NEW)
│   └── wwwroot/
│       ├── index.html
│       └── css/
│           └── app.css
│
├── Docly.*.Tests/          # Test projects
│
└── Docly.slnx              # Solution file
```

## API Endpoints

### Authentication
- `POST /api/auth/login` - Login
- `POST /api/auth/register` - Register

### Documents
- `GET /api/documents/` - List documents
- `POST /api/documents/` - Upload document
- `GET /api/search/?query=X` - Search

### Chat (Stateless)
- `POST /api/chat/` - Ask question (auto-saves to history)

### Chat History (NEW)
- `GET /api/conversations/` - List conversations
- `POST /api/conversations/` - Create conversation
- `GET /api/conversations/{id}` - Get conversation
- `PATCH /api/conversations/{id}` - Update conversation
- `DELETE /api/conversations/{id}` - Delete conversation
- `GET /api/conversations/search?q=X` - Search conversations
- `GET /api/conversations/{id}/messages` - Get messages
- `POST /api/conversations/{id}/messages` - Add message

## Testing the Implementation

### 1. Test API Endpoints (using curl or Postman)

```bash
# Register
curl -X POST https://localhost:7000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"TestPassword123"}'

# Login
curl -X POST https://localhost:7000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"TestPassword123"}'

# Save token from response
TOKEN="your-jwt-token-here"

# Create conversation
curl -X POST https://localhost:7000/api/conversations/ \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"title":"Test Conversation"}'

# List conversations
curl -X GET https://localhost:7000/api/conversations/ \
  -H "Authorization: Bearer $TOKEN"

# Ask a question (with chat history)
curl -X POST https://localhost:7000/api/chat/ \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"question":"What is this?","conversationId":"<uuid>"}'
```

### 2. Test Blazor Frontend

1. Navigate to https://localhost:7001
2. Register a new account
3. Upload a test document
4. Wait for indexing to complete
5. Ask a question
6. Verify conversation is saved in ChatHistory
7. Refresh browser - verify login persists (localStorage)
8. Switch to different conversation
9. Search conversations by title

## Troubleshooting

### "Connection refused" on PostgreSQL
- Ensure PostgreSQL is running
- Check connection string in appsettings
- Default: `Host=localhost;Username=postgres;Password=<your-password>`

### "Embedding service unavailable"
- Ensure Ollama is running: `ollama serve`
- Verify models are pulled: `ollama list`
- Check Ollama URL in appsettings (default: http://localhost:11434)

### "401 Unauthorized" on API calls
- Check JWT token is valid and not expired
- Ensure Authorization header: `Bearer <token>`
- Verify JWT secret in appsettings matches between API runs

### localStorage not working in Blazor
- This requires JS interop (implemented in AuthService)
- Ensure browser allows localStorage
- Check browser DevTools → Application → Storage → Local Storage

### Migrations failing
- Ensure database exists: `CREATE DATABASE docly;`
- Ensure pgvector extension: `CREATE EXTENSION vector;`
- Try fresh migration: `dotnet ef database drop --force` then `update`

## Production Deployment

### Docker Setup

```dockerfile
# Dockerfile for API
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY Docly.Api/bin/Release/net10.0/publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "Docly.Api.dll"]
```

```bash
# Build
docker build -t docly-api:latest .

# Run
docker run -p 8080:80 \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=docly;..." \
  -e Ollama__BaseUrl="http://ollama:11434" \
  docly-api:latest
```

### Docker Compose

```yaml
version: '3.8'
services:
  api:
    build: .
    ports:
      - "7000:80"
    environment:
      ConnectionStrings__DefaultConnection: "Host=postgres;Database=docly;Username=postgres;Password=docly"
      Ollama__BaseUrl: "http://ollama:11434"
    depends_on:
      - postgres
      - ollama

  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_PASSWORD: docly
      POSTGRES_DB: docly
    volumes:
      - postgres_data:/var/lib/postgresql/data
    ports:
      - "5432:5432"

  ollama:
    image: ollama/ollama:latest
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama

volumes:
  postgres_data:
  ollama_data:
```

## Performance Tuning

### Database
- Add indexes on frequently searched columns (done in migrations)
- Use connection pooling (default in EF Core)
- Consider read replicas for scaling

### API
- Enable response caching for documents
- Batch operations where possible
- Implement pagination for message lists

### Frontend
- Lazy load conversation history
- Virtual scrolling for large message lists
- Service Worker for offline support (future)

## Monitoring & Logging

### Application Logs
```csharp
// In Program.cs
builder.Logging.AddConsole();
builder.Logging.AddDebug();
```

### Database Monitoring
```sql
-- Slow queries
SELECT * FROM pg_stat_statements ORDER BY mean_time DESC LIMIT 10;

-- Table sizes
SELECT schemaname, tablename, pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) 
FROM pg_tables ORDER BY pg_total_relation_size DESC;
```

## Security Considerations

✅ **Implemented**
- JWT authentication
- Password hashing (BCrypt)
- Rate limiting
- HTTPS/TLS ready
- User isolation (query by UserId)
- SQL injection prevention (EF Core parameterized queries)

⚠️ **To Add**
- CSRF protection (for forms)
- Refresh token rotation
- API key authentication (for integrations)
- Audit logging
- Two-factor authentication

## Next Steps

1. **Run locally** - Follow dev setup above
2. **Test endpoints** - Use curl/Postman examples
3. **Deploy** - Use Docker Compose or cloud platform
4. **Monitor** - Set up logging & alerting
5. **Scale** - Add caching, read replicas, CDN

## References

- [Blazor WebAssembly Docs](https://learn.microsoft.com/aspnet/core/blazor)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- [pgvector](https://github.com/pgvector/pgvector)
- [Ollama](https://ollama.ai/)

---

**Last Updated**: 2026-07-23
**Status**: Ready for deployment
