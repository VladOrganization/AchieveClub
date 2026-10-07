namespace AchieveClub.Server.ApiContracts.Achievements.Response;

public record AchievementResponse(
    int Id,
    int Xp,
    string Title,
    string Description,
    string LogoURL,
    int? TagId
);