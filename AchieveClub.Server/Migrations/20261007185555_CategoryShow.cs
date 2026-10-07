using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AchieveClub.Server.Migrations
{
    /// <inheritdoc />
    public partial class CategoryShow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Show",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Show",
                table: "Categories");
        }
    }
}
