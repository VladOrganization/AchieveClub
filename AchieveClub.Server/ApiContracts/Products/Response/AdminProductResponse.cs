namespace AchieveClub.Server.ApiContracts.Products.Response;

public record AdminVariantResponse(int Id, string Name, string Color, int Quantity, bool Default, List<VariantPhotoResponse> Photos);

public record AdminProductResponse(int Id, string Type, string Name, string Details, int Price, int CategoryId, string? CategoryTitle, List<AdminVariantResponse> Variants);
