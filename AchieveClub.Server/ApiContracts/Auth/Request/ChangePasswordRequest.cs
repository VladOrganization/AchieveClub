using System.ComponentModel.DataAnnotations;

namespace AchieveClub.Server.ApiContracts.Auth.Request
{
    public record ChangePasswordRequest(
        [Required, EmailAddress] string EmailAddress,
        [Required, Range(1000, 9999)] int ProofCode,
        [Required, StrongPassword] string Password
    );
}
