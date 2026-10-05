using System.ComponentModel.DataAnnotations;

namespace AchieveClub.Server.ApiContracts.Auth.Request
{
    public record GoogleAuthRequest([Required, MinLength(100), MaxLength(8000)] string IdToken);
}
