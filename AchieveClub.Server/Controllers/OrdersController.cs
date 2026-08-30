using AchieveClub.Server.ApiContracts.Orders.Request;
using AchieveClub.Server.ApiContracts.Orders.Response;
using AchieveClub.Server.RepositoryItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace AchieveClub.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController(ILogger<OrdersController> logger, ApplicationContext db, IOutputCacheStore cache) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<List<OrderResponse>>> GetUserOrders()
    {
        var userIdString = HttpContext.User.Identity?.Name;
        if (userIdString == null || int.TryParse(userIdString, out int userId) == false)
        {
            logger.LogWarning("Access token not contains userId or userId is the wrong format: {userIdString}",
                userIdString);
            return NotFound($"Access token not contains userId or userId is the wrong format: {userIdString}");
        }

        if (await db.Users.AnyAsync(u => u.Id == userId) == false)
        {
            logger.LogWarning("User with userId:{userId} not found", userId);
            return NotFound($"User with userId:{userId} not found");
        }

        return await db.Orders
            .Where(o => o.UserId == userId)
            .Include(o=>o.Product)
            .Include(o=>o.DeliveryStatus)
            .Include(o=>o.Variant)
            .ThenInclude(v=>v!.DefaultPhoto)
            .Select(o => new OrderResponse(o.Id, o.Product!.Type, o.Product.Name, o.Price, o.Variant!.Name,
                o.Variant.DefaultPhoto!.Url, o.OrderDate, o.DeliveryStatus!.Title, o.DeliveryStatus.Color))
            .ToListAsync();
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var userIdString = HttpContext.User.Identity?.Name;
        if (userIdString == null || int.TryParse(userIdString, out int userId) == false)
        {
            logger.LogWarning("Access token not contains userId or userId is the wrong format: {userIdString}",
                userIdString);
            return NotFound($"Access token not contains userId or userId is the wrong format: {userIdString}");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            logger.LogWarning("User with userId:{userId} not found", userId);
            return NotFound($"User with userId:{userId} not found");
        }

        var variant = await db.Variants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == request.variantId && v.ProductId == request.productId);

        if (variant == null)
        {
            logger.LogWarning("Product:{request.productId} with Variant:{request.variantId} not found",
                request.productId, request.variantId);
            return NotFound($"Product:{request.productId} with Variant:{request.variantId} not found");
        }

        if (user.Balance < variant.Product!.Price)
        {
            logger.LogWarning(
                "The current user does not have enough money to order the product. Balance:{user.Balance} < Price:{variant.Product.Price}",
                user.Balance, variant.Product.Price);
            return BadRequest(
                $"The current user does not have enough money to order the product. Balance:{user.Balance} < Price:{variant.Product.Price}");
        }

        if (variant.Quantity <= 0)
        {
            logger.LogWarning(
                "Out of stock. Product:{request.productId} Variant:{request.variantId}",
                request.productId, request.variantId);
            return BadRequest(
                $"Out of stock. Product:{request.productId} Variant:{request.variantId}");
        }

        user.Balance -= variant.Product!.Price;

        variant.Quantity--;

        var order = new OrderDBO
        {
            OrderDate = DateTime.Now,
            Price = variant.Product!.Price,
            User = user,
            Product = variant.Product,
            Variant = variant,
            DeliveryStatusId = 1
        };
        db.Orders.Add(order);

        await db.SaveChangesAsync();
        await cache.EvictByTagAsync("achievements", CancellationToken.None);

        return Created();
    }

    [HttpGet("all")]
    [Authorize(Roles = "Supervisor, Admin")]
    public async Task<ActionResult<List<AdminOrderResponse>>> GetAllOrders(
        [FromQuery] int? statusId,
        [FromQuery] int? userId)
    {
        var query = db.Orders
            .Include(o => o.Product)
            .Include(o => o.DeliveryStatus)
            .Include(o => o.User)
            .Include(o => o.Variant)
            .ThenInclude(v => v!.DefaultPhoto)
            .AsQueryable();

        if (statusId.HasValue)
            query = query.Where(o => o.DeliveryStatusId == statusId.Value);

        if (userId.HasValue)
            query = query.Where(o => o.UserId == userId.Value);

        return await query
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new AdminOrderResponse(
                o.Id,
                o.UserId,
                o.User!.FirstName,
                o.User.LastName,
                o.Product!.Type,
                o.Product.Name,
                o.Price,
                o.Variant!.Name,
                o.Variant.DefaultPhoto != null ? o.Variant.DefaultPhoto.Url : null,
                o.OrderDate,
                o.DeliveryStatusId,
                o.DeliveryStatus!.Title,
                o.DeliveryStatus.Color))
            .ToListAsync();
    }

    [HttpPatch("{orderId:int}/status")]
    [Authorize(Roles = "Supervisor, Admin")]
    public async Task<ActionResult> ChangeStatus([FromRoute] int orderId, [FromBody] ChangeOrderStatusRequest request)
    {
        var order = await db.Orders
            .Include(o => o.DeliveryStatus)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            logger.LogWarning("Order:{orderId} not found", orderId);
            return NotFound($"Order:{orderId} not found");
        }

        if (DeliveryStatusNames.IsCancelled(order.DeliveryStatus?.Title))
        {
            logger.LogWarning("Order:{orderId} is already cancelled", orderId);
            return Conflict("order cancelled");
        }

        var status = await db.DeliveryStatuses.FirstOrDefaultAsync(s => s.Id == request.StatusId);
        if (status == null)
        {
            logger.LogWarning("Delivery status:{statusId} not found", request.StatusId);
            return NotFound($"Delivery status:{request.StatusId} not found");
        }

        if (DeliveryStatusNames.IsCancelled(status.Title))
        {
            return BadRequest("Use cancel endpoint to reject an order and refund XP");
        }

        order.DeliveryStatusId = status.Id;
        await db.SaveChangesAsync();

        logger.LogInformation("Order:{orderId} status changed to {statusId} ({title})", orderId, status.Id, status.Title);
        return NoContent();
    }

    [HttpPost("{orderId:int}/cancel")]
    [Authorize(Roles = "Supervisor, Admin")]
    public async Task<ActionResult> CancelOrder([FromRoute] int orderId)
    {
        var order = await db.Orders
            .Include(o => o.User)
            .Include(o => o.Variant)
            .Include(o => o.DeliveryStatus)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            logger.LogWarning("Order:{orderId} not found", orderId);
            return NotFound($"Order:{orderId} not found");
        }

        if (DeliveryStatusNames.IsCancelled(order.DeliveryStatus?.Title))
        {
            logger.LogWarning("Order:{orderId} is already cancelled", orderId);
            return Conflict("order cancelled");
        }

        if (DeliveryStatusNames.IsReceived(order.DeliveryStatus?.Title))
        {
            logger.LogWarning("Order:{orderId} is already received", orderId);
            return Conflict("order already received");
        }

        await using var tx = await db.Database.BeginTransactionAsync();

        var cancelledStatus = await DeliveryStatusNames.EnsureCancelledStatusAsync(db);

        if (order.User != null)
            order.User.Balance += order.Price;

        if (order.Variant != null)
            order.Variant.Quantity++;

        order.DeliveryStatus = cancelledStatus;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        await cache.EvictByTagAsync("achievements", CancellationToken.None);

        logger.LogInformation(
            "Order:{orderId} cancelled. Refunded {price} XP to user:{userId}",
            orderId, order.Price, order.UserId);

        return NoContent();
    }
}