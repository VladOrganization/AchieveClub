namespace AchieveClub.Server.ApiContracts.Orders.Response;

public record AdminOrderResponse(
    int Id, DateTime OrderDate, int Price,
    int UserId, string UserFirstName, string UserLastName, string UserEmail,
    int ProductId, string ProductType, string ProductTitle,
    int VariantId, string VariantName, string VariantColor, string? Photo,
    int DeliveryStatusId, string DeliveryStatus, string DeliveryColor);

public record DeliveryStatusResponse(int Id, string Title, string Color);
