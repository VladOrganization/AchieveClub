using AchieveClub.Server.ApiContracts.Orders.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AchieveClub.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeliveryStatusesController(ApplicationContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Supervisor, Admin")]
    public async Task<ActionResult<List<DeliveryStatusResponse>>> GetAll()
    {
        await DeliveryStatusNames.EnsureReceivedStatusAsync(db);

        return await db.DeliveryStatuses
            .OrderBy(s => s.Id)
            .Select(s => new DeliveryStatusResponse(s.Id, s.Title, s.Color))
            .ToListAsync();
    }
}
