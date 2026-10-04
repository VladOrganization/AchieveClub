using AchieveClub.Server.ApiContracts.Orders.Request;
using AchieveClub.Server.ApiContracts.Orders.Response;
using AchieveClub.Server.RepositoryItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AchieveClub.Server.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = "Admin")]
public class AdminOrdersController(ILogger<AdminOrdersController> logger, ApplicationContext db) : ControllerBase
{
    private const string CancelledStatusTitle = "Отменён";

    [HttpGet]
    public async Task<ActionResult<List<AdminOrderResponse>>> GetAll(CancellationToken ct)
    {
        return await db.Orders
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new AdminOrderResponse(
                o.Id, o.OrderDate, o.Price,
                o.UserId, o.User!.FirstName, o.User.LastName, o.User.Email,
                o.ProductId, o.Product!.Type, o.Product.Name,
                o.VariantId, o.Variant!.Name, o.Variant.Color, o.Variant.DefaultPhoto!.Url,
                o.DeliveryStatusId, o.DeliveryStatus!.Title, o.DeliveryStatus.Color))
            .ToListAsync(ct);
    }

    [HttpGet("statuses")]
    public async Task<ActionResult<List<DeliveryStatusResponse>>> GetStatuses(CancellationToken ct)
    {
        return await db.DeliveryStatuses
            .OrderBy(s => s.Id)
            .Select(s => new DeliveryStatusResponse(s.Id, s.Title, s.Color))
            .ToListAsync(ct);
    }

    [HttpPatch("{orderId:int}/status")]
    public async Task<ActionResult> ChangeStatus([FromRoute] int orderId, [FromBody] ChangeOrderStatusRequest request, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.DeliveryStatus).FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order == null)
        {
            logger.LogWarning("Order with orderId:{orderId} not found", orderId);
            return NotFound($"Order with orderId:{orderId} not found");
        }

        if (order.DeliveryStatus?.Title == CancelledStatusTitle)
            return BadRequest($"Order {orderId} is cancelled and its status cannot be changed");

        var newStatus = await db.DeliveryStatuses.FirstOrDefaultAsync(s => s.Id == request.DeliveryStatusId, ct);
        if (newStatus == null)
        {
            logger.LogWarning("Delivery status with id:{statusId} not found", request.DeliveryStatusId);
            return BadRequest($"Delivery status with id:{request.DeliveryStatusId} not found");
        }

        if (newStatus.Title == CancelledStatusTitle)
            return BadRequest("Use the cancel endpoint to cancel an order");

        order.DeliveryStatusId = request.DeliveryStatusId;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Order {orderId} status changed to {statusId}", orderId, request.DeliveryStatusId);
        return NoContent();
    }

    [HttpPost("{orderId:int}/cancel")]
    public async Task<ActionResult> Cancel([FromRoute] int orderId, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Variant)
            .Include(o => o.User)
            .Include(o => o.DeliveryStatus)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order == null)
        {
            logger.LogWarning("Order with orderId:{orderId} not found", orderId);
            return NotFound($"Order with orderId:{orderId} not found");
        }

        if (order.DeliveryStatus?.Title == CancelledStatusTitle)
        {
            logger.LogWarning("Order {orderId} is already cancelled", orderId);
            return BadRequest($"Order {orderId} is already cancelled");
        }

        var cancelledStatus = await db.DeliveryStatuses.FirstOrDefaultAsync(s => s.Title == CancelledStatusTitle, ct);
        if (cancelledStatus == null)
        {
            cancelledStatus = new DeliveryStatusDBO { Title = CancelledStatusTitle, Color = "#dc2626" };
            db.DeliveryStatuses.Add(cancelledStatus);
        }

        if (order.Variant != null)
            order.Variant.Quantity++;

        if (order.User != null)
            order.User.Balance += order.Price;

        order.DeliveryStatus = cancelledStatus;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Order {orderId} cancelled, variant {variantId} quantity restored, {price} refunded to user {userId}",
            orderId, order.VariantId, order.Price, order.UserId);
        return NoContent();
    }
}
