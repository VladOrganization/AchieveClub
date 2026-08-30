using AchieveClub.Server.ApiContracts.Balance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AchieveClub.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BalanceController(ILogger<BalanceController> logger, ApplicationContext db) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<int>> GetCurrentUserBalance()
    {
        var userIdString = HttpContext.User.Identity?.Name;
        if (userIdString == null || int.TryParse(userIdString, out int userId) == false)
        {
            logger.LogWarning("Access token not contains userId or userId is the wrong format: {userIdString}", userIdString);
            return NotFound($"Access token not contains userId or userId is the wrong format: {userIdString}");
        }
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            logger.LogWarning("User with userId:{userId} not found", userId);
            return NotFound($"User with userId:{userId} not found");
        }

        return user.Balance;
    }

    [HttpGet("{userId:int}")]
    [Authorize(Roles = "Supervisor, Admin")]
    public async Task<ActionResult<int>> GetUserBalance([FromRoute] int userId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            logger.LogWarning("User with userId:{userId} not found", userId);
            return NotFound($"User with userId:{userId} not found");
        }

        return user.Balance;
    }

    [HttpPatch("{userId:int}")]
    [Authorize(Roles = "Supervisor, Admin")]
    public async Task<ActionResult<int>> ChangeUserBalance([FromRoute] int userId, [FromBody] ChangeBalanceRequest request)
    {
        var actorId = HttpContext.User.Identity?.Name;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            logger.LogWarning("User with userId:{userId} not found", userId);
            return NotFound($"User with userId:{userId} not found");
        }

        if (request.Balance < 0)
        {
            logger.LogWarning("Rejected negative balance {balance} for user:{userId}", request.Balance, userId);
            return BadRequest("Balance cannot be negative");
        }

        var previous = user.Balance;
        user.Balance = request.Balance;
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Balance changed by {actorId} for user:{userId} from {previous} to {next}",
            actorId, userId, previous, request.Balance);

        return user.Balance;
    }
}