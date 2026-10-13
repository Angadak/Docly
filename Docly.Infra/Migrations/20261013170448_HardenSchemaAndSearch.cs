using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Docly.Infra.Migrations
{
    /// <inheritdoc />
    public partial class HardenSchemaAndSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ConversationId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_CreatedAtUtc",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatConversations_CreatedAtUtc",
                table: "ChatConversations");

            migrationBuilder.DropIndex(
                name: "IX_ChatConversations_UserId",
                table: "ChatConversations");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            // Existing error messages may be longer than the new column limit.
            migrationBuilder.Sql("""UPDATE "Documents" SET "ProcessingError" = LEFT("ProcessingError", 1000) WHERE length("ProcessingError") > 1000;""");

            migrationBuilder.AlterColumn<string>(
                name: "ProcessingError",
                table: "Documents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "Documents",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Status",
                table: "Documents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UserId_CreatedAtUtc",
                table: "Documents",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_Embedding",
                table: "DocumentChunks",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ConversationId_CreatedAtUtc",
                table: "ChatMessages",
                columns: new[] { "ConversationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_UserId_UpdatedAtUtc",
                table: "ChatConversations",
                columns: new[] { "UserId", "UpdatedAtUtc" });

            // Embeddings now use nomic-embed-text's "search_document:" / "search_query:" prefixes.
            // Re-queue indexed documents so their chunks are re-embedded consistently with new queries.
            migrationBuilder.Sql("""UPDATE "Documents" SET "Status" = 'PendingExtraction', "ProcessingError" = NULL WHERE "Status" = 'Completed';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_Status",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_UserId_CreatedAtUtc",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_Embedding",
                table: "DocumentChunks");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ConversationId_CreatedAtUtc",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatConversations_UserId_UpdatedAtUtc",
                table: "ChatConversations");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "ProcessingError",
                table: "Documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "Documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ConversationId",
                table: "ChatMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_CreatedAtUtc",
                table: "ChatMessages",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_CreatedAtUtc",
                table: "ChatConversations",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_UserId",
                table: "ChatConversations",
                column: "UserId");
        }
    }
}
