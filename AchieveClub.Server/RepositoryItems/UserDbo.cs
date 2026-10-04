using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AchieveClub.Server.ApiContracts.Users;

namespace AchieveClub.Server.RepositoryItems;

[Table("Users")]
public class UserDbo
{
    public int Id { get; set; }
    [MaxLength(100)] public required string FirstName { get; set; }
    [MaxLength(100)] public required string LastName { get; set; }
    [MaxLength(500)] public required string Email { get; set; }
    [MaxLength(256)] public required string Password { get; set; }
    [MaxLength(4000)] public string? Avatar { get; set; }
    public int Balance { get; set; }
    [MaxLength(256)] public string? RefreshToken { get; set; }
    public int RoleRefId { get; set; }
    [ForeignKey(nameof(RoleRefId))] public required RoleDbo Role { get; set; }

    public UserResponse ToUserState(int xpSum) => new UserResponse(Id, FirstName, LastName, Avatar, xpSum);
    public CurrentUserResponse ToCurrentUserState(int xpSum) => new CurrentUserResponse(Id, FirstName, LastName, Avatar, xpSum, Email);
}