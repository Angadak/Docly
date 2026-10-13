using System.Text.Json;
using Docly.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Docly.Infra.DbContexts
{
    public class AppDbContext : DbContext
    {
        public const int EmbeddingDimensions = 768;

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Document> Documents => Set<Document>();
        public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
        public DbSet<User> Users => Set<User>();
        public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Fresh databases need pgvector before the DocumentChunks.Embedding column can be created.
            modelBuilder.HasPostgresExtension("vector");

            modelBuilder.Entity<Document>(entity =>
            {
                entity.ToTable("Documents");
                entity.HasKey(d => d.Id);
                entity.Property(d => d.Status)
                      .HasConversion<string>()
                      .HasMaxLength(50)
                      .IsRequired();
                entity.Property(d => d.ContentType).IsRequired().HasMaxLength(Document.MaxContentTypeLength);
                entity.Property(d => d.Filename).IsRequired().HasMaxLength(Document.MaxFilenameLength);
                entity.Property(d => d.Title).IsRequired().HasMaxLength(Document.MaxTitleLength);
                entity.Property(d => d.CreatedAtUtc).IsRequired();
                entity.Property(d => d.StorageKey).IsRequired().HasMaxLength(255);
                entity.Property(d => d.FileSizeBytes).IsRequired();
                entity.Property(d => d.ProcessingError).HasMaxLength(Document.MaxProcessingErrorLength).IsRequired(false);
                entity.Ignore(d => d.IsProcessing);

                entity.HasIndex(d => new { d.UserId, d.CreatedAtUtc });
                entity.HasIndex(d => d.Status);
            });

            modelBuilder.Entity<DocumentChunk>(entity =>
            {
                entity.ToTable("DocumentChunks");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Content).IsRequired();
                entity.Property(c => c.ChunkIndex).IsRequired();
                entity.Property(c => c.CreatedAtUtc).IsRequired();
                entity.HasIndex(c => c.DocumentId);
                entity.HasOne<Document>()
                    .WithMany()
                    .HasForeignKey(c => c.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(c => c.Embedding)
                    .HasConversion(
                        v => v == null ? null : new Pgvector.Vector(v),
                        v => v == null ? null : v.ToArray())
                    .HasColumnType($"vector({EmbeddingDimensions})")
                    .Metadata.SetValueComparer(new ValueComparer<float[]?>(
                        (e1, e2) => (e1 ?? Array.Empty<float>()).SequenceEqual(e2 ?? Array.Empty<float>()),
                        e => e == null ? 0 : e.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        e => e == null ? null : e.ToArray()));

                // Approximate nearest-neighbour index so search doesn't scan every chunk.
                entity.HasIndex(c => c.Embedding)
                    .HasMethod("hnsw")
                    .HasOperators("vector_cosine_ops");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Email).IsRequired().HasMaxLength(User.MaxEmailLength);
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.PasswordHash).IsRequired();
                entity.Property(u => u.CreatedAtUtc).IsRequired();
            });

            modelBuilder.Entity<ChatConversation>(entity =>
            {
                entity.ToTable("ChatConversations");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.UserId).IsRequired();
                entity.Property(c => c.Title).IsRequired().HasMaxLength(ChatConversation.MaxTitleLength);
                entity.Property(c => c.Tags)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v ?? new List<string>(), (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                    .HasColumnType("jsonb")
                    .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                        (c1, c2) => (c1 ?? new()).SequenceEqual(c2 ?? new()),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToList()));
                entity.Property(c => c.CreatedAtUtc).IsRequired();
                entity.Property(c => c.UpdatedAtUtc).IsRequired();
                entity.HasIndex(c => new { c.UserId, c.UpdatedAtUtc });
            });

            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.ToTable("ChatMessages");
                entity.HasKey(m => m.Id);
                entity.Property(m => m.ConversationId).IsRequired();
                entity.Property(m => m.Role).IsRequired().HasMaxLength(20);
                entity.Property(m => m.Content).IsRequired();
                entity.Property(m => m.SourceDocuments)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v ?? new List<SourceDocument>(), (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<SourceDocument>>(v, (JsonSerializerOptions?)null) ?? new List<SourceDocument>())
                    .HasColumnType("jsonb")
                    .Metadata.SetValueComparer(new ValueComparer<List<SourceDocument>?>(
                        (s1, s2) => (s1 ?? new()).SequenceEqual(s2 ?? new()),
                        s => s == null ? 0 : s.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        s => s == null ? null : s.ToList()));
                entity.Property(m => m.CreatedAtUtc).IsRequired();
                entity.HasIndex(m => new { m.ConversationId, m.CreatedAtUtc });
                entity.HasOne<ChatConversation>()
                    .WithMany()
                    .HasForeignKey(m => m.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
