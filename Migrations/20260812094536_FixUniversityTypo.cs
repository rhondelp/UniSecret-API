using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniSecret.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixUniversityTypo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreateAt",
                table: "Universities",
                newName: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Universities",
                newName: "CreateAt");
        }
    }
}
