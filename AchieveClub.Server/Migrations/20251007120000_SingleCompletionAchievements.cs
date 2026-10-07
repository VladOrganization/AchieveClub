using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AchieveClub.Server.Migrations
{
    /// <inheritdoc />
    public partial class SingleCompletionAchievements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep only the earliest completion for each (user, achievement) pair.
            migrationBuilder.Sql("""
                DELETE FROM "CompletedAchievements" ca
                USING "CompletedAchievements" other
                WHERE ca."UserRefId" = other."UserRefId"
                  AND ca."AchieveRefId" = other."AchieveRefId"
                  AND (ca."DateOfCompletion" > other."DateOfCompletion"
                       OR (ca."DateOfCompletion" = other."DateOfCompletion" AND ca."Id" > other."Id"));
                """);

            migrationBuilder.DropColumn(
                name: "IsMultiple",
                table: "Achievements");

            migrationBuilder.DropIndex(
                name: "IX_CompletedAchievements_UserRefId",
                table: "CompletedAchievements");

            migrationBuilder.CreateIndex(
                name: "IX_CompletedAchievements_UserRefId_AchieveRefId",
                table: "CompletedAchievements",
                columns: new[] { "UserRefId", "AchieveRefId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompletedAchievements_UserRefId_AchieveRefId",
                table: "CompletedAchievements");

            migrationBuilder.CreateIndex(
                name: "IX_CompletedAchievements_UserRefId",
                table: "CompletedAchievements",
                column: "UserRefId");

            migrationBuilder.AddColumn<bool>(
                name: "IsMultiple",
                table: "Achievements",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
