# AI-Powered Document Assistant Roadmap

Last reviewed: 2026-07-17

## Phase 1 — Foundation

- [x] Project skeleton and Clean Architecture setup
- [x] PostgreSQL setup, EF Core, and Documents migrations
- [~] Document upload API
  - Endpoint: `POST /api/documents`
  - Local file storage and document metadata persistence are implemented.
  - Remaining: confirm one successful multipart upload returns `201 Created`, creates a database row, and writes the physical file.

## Phase 2 — Core AI Integration

- [ ] Text extraction pipeline (PDF/DOCX parsing)
- [ ] Chunking strategy for large documents
- [ ] OpenAI embeddings generation and pgvector storage
- [ ] Semantic-search endpoint (vector similarity)
- [ ] RAG chat endpoint

## Phase 3 — Production Hardening

- [ ] Authentication and authorization
- [ ] Async processing/background jobs for extraction and embeddings
- [ ] Logging, error handling, and observability
- [ ] Rate limiting and OpenAI cost control

## Phase 4 — Azure Deployment

- [ ] Containerization (Docker)
- [ ] Azure infrastructure: application hosting, PostgreSQL, and Key Vault
- [ ] CI/CD pipeline

## Immediate next step

Complete and verify the upload API. After that, proceed to text extraction. The upload endpoint should store only the original file and metadata; extraction should become a separate service so PDF and DOCX parsing do not leak into the API endpoint.

## Current upload test checklist

1. Start the API with the Development launch profile.
2. Send `POST /api/documents` as `multipart/form-data` with `file` and `title`.
3. Confirm `201 Created`.
4. Confirm the file exists under `FileStorage:RootPath`.
5. Confirm its metadata exists in the `Documents` database table.
