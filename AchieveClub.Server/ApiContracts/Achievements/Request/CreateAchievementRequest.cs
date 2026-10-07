using System.ComponentModel.DataAnnotations;

namespace AchieveClub.Server.ApiContracts.Achievements.Request
{
    public record CreateAchievementRequest(
        [Required, StringLength(100, MinimumLength = 5)]
        string Title,
        [Required, StringLength(300, MinimumLength = 5)]
        string Description,
        [Required] string LogoURL,
        [Required, Range(0, double.MaxValue)] int Xp
    );
}