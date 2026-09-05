using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Documents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "documents");

            migrationBuilder.CreateTable(
                name: "rendered_documents",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    template_version = table.Column<int>(type: "integer", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    rendered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rendered_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rendered_documents", x => x.id);
                    table.CheckConstraint("ck_rendered_documents_sha256", "length(sha256) = 64");
                    table.CheckConstraint("ck_rendered_documents_size_bytes", "size_bytes > 0");
                    table.CheckConstraint("ck_rendered_documents_type", "type IN ('Invoice')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_rendered_documents_rendered_at",
                schema: "documents",
                table: "rendered_documents",
                column: "rendered_at");

            migrationBuilder.CreateIndex(
                name: "ux_rendered_documents_type_reference",
                schema: "documents",
                table: "rendered_documents",
                columns: new[] { "type", "reference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rendered_documents",
                schema: "documents");
        }
    }
}
