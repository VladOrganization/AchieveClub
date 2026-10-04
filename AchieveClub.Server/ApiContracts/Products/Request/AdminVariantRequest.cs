namespace AchieveClub.Server.ApiContracts.Products.Request;

/// <param name="Id">Id существующего варианта; null - создать новый вариант</param>
/// <param name="Photos">URL фото варианта</param>
/// <param name="DefaultPhotoIndex">Индекс главного фото в списке Photos</param>
public record AdminVariantRequest(int? Id, string Name, string Color, int Quantity, List<string> Photos, int DefaultPhotoIndex = 0);
