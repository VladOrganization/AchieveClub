using System.ComponentModel.DataAnnotations;

namespace AchieveClub.Server.ApiContracts.Avatars.Request
{
    public record SelectAvatarPresetRequest(
        [Required, MaxLength(4000)] string Path
    );
}
