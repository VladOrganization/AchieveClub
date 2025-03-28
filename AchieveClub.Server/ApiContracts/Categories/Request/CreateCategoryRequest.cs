namespace AchieveClub.Server.ApiContracts.Categories.Request
{
    public record CreateCategoryRequest(string Title, string? Color, DateTime? StartDate, DateTime? EndDate, string? AvailableBanner, string? UnavailableBanner);
}
