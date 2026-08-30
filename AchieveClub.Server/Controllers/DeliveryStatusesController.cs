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
        try
        {
            await DeliveryStatusNames.EnsureReceivedStatusAsync(db);
        }
        catch (DbUpdateException)
        {
            // Sequence/PK races must not block the statuses list.
        }

        return await db.DeliveryStatuses
            .OrderBy(s => s.Id)
            .Select(s => new DeliveryStatusResponse(s.Id, s.Title, s.Color))
            .ToListAsync();
    }
}
