namespace AchieveClub.Server.ApiContracts.Auth.Response
{
    public record TokenPairResponce(int UserId, string AuthToken, string RefreshToken, long Expire, int Role);
}
