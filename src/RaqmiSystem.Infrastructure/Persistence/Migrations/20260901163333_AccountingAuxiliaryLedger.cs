using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountingAuxiliaryLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "party_id",
                schema: "accounting",
                table: "journal_entry_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_lines_party_id",
                schema: "accounting",
                table: "journal_entry_lines",
                column: "party_id");

            migrationBuilder.AddForeignKey(
                name: "FK_journal_entry_lines_parties_party_id",
                schema: "accounting",
                table: "journal_entry_lines",
                column: "party_id",
                principalSchema: "accounting",
                principalTable: "parties",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_journal_entry_lines_parties_party_id",
                schema: "accounting",
                table: "journal_entry_lines");

            migrationBuilder.DropIndex(
                name: "IX_journal_entry_lines_party_id",
                schema: "accounting",
                table: "journal_entry_lines");

            migrationBuilder.DropColumn(
                name: "party_id",
                schema: "accounting",
                table: "journal_entry_lines");
        }
    }
}
