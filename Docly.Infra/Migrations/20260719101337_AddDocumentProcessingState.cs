using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Docly.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentProcessingState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProcessingError",
                table: "Documents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessingError",
                table: "Documents");
        }
    }
}
