using System.ComponentModel.DataAnnotations;

namespace AchieveClub.Server.ApiContracts.Auth.Request
{
    public record RegistrationRequest(
            [Required, StringLength(100, MinimumLength = 2)] string FirstName,
            [Required, StringLength(100, MinimumLength = 4)] string LastName,
            [Required, MinLength(6), MaxLength(100)] string Password,
            string? AvatarURL,
            [Required, EmailAddress] string EmailAddress,
            [Required, Range(1000, 9999)] int ProofCode
    );
}
