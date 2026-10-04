namespace AchieveClub.Server.ApiContracts.Users;

public record CurrentUserResponse(
    int Id,
    string FirstName,
    string LastName,
    string? Avatar,
    int XpSum,
    string Email);