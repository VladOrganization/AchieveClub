namespace AchieveClub.Server.ApiContracts.Categories.Response;

public record AdminCategoryResponse(
    int Id,
    string Title,
    string? Color,
    DateTime? StartDate,
    DateTime? EndDate,
    string? AvailableBanner,
    string? UnavailableBanner,
    bool Show,
    int ProductsCount);
