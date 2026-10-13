using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Docly.Infra.Migrations
{
    /// <inheritdoc />
    public partial class UpdateValueComparers : Migration
    {
        // This migration originally re-created ChatConversations/ChatMessages, which AddChatHistory had
        // already created, so it failed on every database. The tables are unchanged; only the model
        // snapshot (value comparers) changed, so Up/Down are intentionally empty.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
