namespace AchieveClub.Server.ApiContracts.Orders.Response;

public record AdminOrderResponse(
    int Id,
    int UserId,
    string FirstName,
    string LastName,
    string ProductType,
    string ProductTitle,
    int Price,
    string Color,
    string? Photo,
    DateTime OrderDate,
    int DeliveryStatusId,
    string DeliveryStatus,
    string DeliveryColor);
