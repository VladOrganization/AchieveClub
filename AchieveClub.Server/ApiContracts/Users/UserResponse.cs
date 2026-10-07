namespace AchieveClub.Server.ApiContracts.Users;

public record UserResponse(
    int Id,
    string FirstName,
    string LastName,
    string? Avatar,
    int XpSum,
    int CompletedCount = 0);