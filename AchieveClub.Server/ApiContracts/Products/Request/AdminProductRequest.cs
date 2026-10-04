namespace AchieveClub.Server.ApiContracts.Products.Request;

/// <param name="DefaultVariantIndex">Индекс варианта по умолчанию в списке Variants</param>
public record AdminProductRequest(string Type, string Name, string Details, int Price, int CategoryId, List<AdminVariantRequest> Variants, int DefaultVariantIndex = 0);
