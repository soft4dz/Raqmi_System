using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Raqmi.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserSiteAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_site_assignments",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    SiteId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_site_assignments", x => new { x.UserId, x.SiteId });
                    table.ForeignKey(
                        name: "FK_user_site_assignments_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_site_assignments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_site_assignments_SiteId",
                table: "user_site_assignments",
                column: "SiteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_site_assignments");
        }
    }
}
