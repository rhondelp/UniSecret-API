using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniSecret.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexesForLikesAndSavedPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SavedPosts_UserId",
                table: "SavedPosts");

            migrationBuilder.DropIndex(
                name: "IX_Likes_UserId",
                table: "Likes");

            migrationBuilder.CreateIndex(
                name: "IX_SavedPosts_UserId_ConfessionId",
                table: "SavedPosts",
                columns: new[] { "UserId", "ConfessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Likes_UserId_LikeableId_LikeableType",
                table: "Likes",
                columns: new[] { "UserId", "LikeableId", "LikeableType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SavedPosts_UserId_ConfessionId",
                table: "SavedPosts");

            migrationBuilder.DropIndex(
                name: "IX_Likes_UserId_LikeableId_LikeableType",
                table: "Likes");

            migrationBuilder.CreateIndex(
                name: "IX_SavedPosts_UserId",
                table: "SavedPosts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_UserId",
                table: "Likes",
                column: "UserId");
        }
    }
}
