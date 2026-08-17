using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniSecret.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInteractiveAndModerationControllers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Confessions_UniversityId",
                table: "Confessions");

            migrationBuilder.CreateIndex(
                name: "IX_Confessions_Status_CreatedAt",
                table: "Confessions",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Confessions_UniversityId_Status_CreatedAt",
                table: "Confessions",
                columns: new[] { "UniversityId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Confessions_Status_CreatedAt",
                table: "Confessions");

            migrationBuilder.DropIndex(
                name: "IX_Confessions_UniversityId_Status_CreatedAt",
                table: "Confessions");

            migrationBuilder.CreateIndex(
                name: "IX_Confessions_UniversityId",
                table: "Confessions",
                column: "UniversityId");
        }
    }
}
