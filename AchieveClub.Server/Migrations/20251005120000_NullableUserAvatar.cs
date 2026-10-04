using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AchieveClub.Server.Migrations
{
    /// <inheritdoc />
    public partial class NullableUserAvatar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Avatar",
                table: "Users",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Users\" SET \"Avatar\" = 'default-avatar.webp' WHERE \"Avatar\" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "Avatar",
                table: "Users",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);
        }
    }
}
