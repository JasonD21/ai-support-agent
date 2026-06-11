using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiSupportAgent.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkHnswIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_chunks_embedding_hnsw " +
                "ON \"Chunks\" USING hnsw (\"Embedding\" vector_cosine_ops);");
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_chunks_embedding_hnsw;");
        }
    }
}
